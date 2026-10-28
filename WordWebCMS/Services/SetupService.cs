using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using MySqlConnector;
using WordWebCMS.Data;
using WordWebCMS.Models;

namespace WordWebCMS.Services;

public sealed class SetupService(IConfiguration config, CmsDb db, StoragePaths storage,
    IDataProtectionProvider protection, ILogger<SetupService> logger)
{
    private string AuthorizationCookie => (config["Site:CookieName"] ?? "WordWebCMS") + ".Setup";
    private readonly ITimeLimitedDataProtector authorization = protection.CreateProtector("WordWebCMS.Setup.v1").ToTimeLimitedDataProtector();
    public SemaphoreSlim Gate { get; } = new(1, 1);
    public bool Configured => !string.IsNullOrWhiteSpace(config.GetConnectionString("Accounts")) &&
        !string.IsNullOrWhiteSpace(config.GetConnectionString("Site"));
    public bool SchemaReady { get; private set; }
    public bool Installed { get; private set; }
    public bool RequiresConfirmation { get; private set; }
    public string Error { get; private set; } = "";
    public string Step { get; set; } = "检查数据库连接";
    public string ConfigurationPath => Path.Combine(storage.Root, "setup.json");

    public void EnsureToken()
    {
        var token = config["Setup:Token"];
        if (string.IsNullOrWhiteSpace(token))
        {
            token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var saved = ReadFile();
            saved["Setup"] = new JsonObject { ["Token"] = token };
            SaveFile(saved);
            config["Setup:Token"] = token;
        }
        // The operator needs this in container logs as well as an interactive console.
        Console.WriteLine("WordWebCMS Setup Token: {0}", token);
    }

    public bool VerifyToken(string token) => !string.IsNullOrWhiteSpace(config["Setup:Token"]) &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(config["Setup:Token"]!));

    public bool IsAuthorized(HttpContext context)
    {
        var cookie = context.Request.Cookies[AuthorizationCookie];
        if (string.IsNullOrEmpty(cookie)) return false;
        try { return VerifyToken(authorization.Unprotect(cookie)); }
        catch (CryptographicException) { return false; }
    }

    public bool Authorize(HttpContext context, string? token)
    {
        if (string.IsNullOrEmpty(token)) return IsAuthorized(context);
        if (!VerifyToken(token)) return false;
        context.Response.Cookies.Append(AuthorizationCookie, authorization.Protect(token, TimeSpan.FromDays(7)), new CookieOptions
        {
            HttpOnly = true, Secure = context.Request.IsHttps, SameSite = SameSiteMode.Strict,
            IsEssential = true, Path = context.Request.PathBase.HasValue ? context.Request.PathBase.Value : "/", MaxAge = TimeSpan.FromDays(7)
        });
        return true;
    }

    public void ForgetAuthorization(HttpContext context) => context.Response.Cookies.Delete(AuthorizationCookie,
        new CookieOptions { Path = context.Request.PathBase.HasValue ? context.Request.PathBase.Value : "/" });

    public void Refresh()
    {
        SchemaReady = false;
        Installed = false;
        if (!Configured) { Error = ""; RequiresConfirmation = false; return; }
        try
        {
            Step = "检查数据库状态";
            Installed = db.Site.DbFirst.ExistsTable("site_settings") && db.Site.Select<SiteSettings>().Any();
            SchemaReady = db.CheckSchema();
            if (SchemaReady) { Error = ""; RequiresConfirmation = false; }
        }
        catch (Exception ex) when (ex is MySqlException or ArgumentException or InvalidOperationException)
        {
            ReportFailure(ex);
        }
    }

    public void Initialize(bool confirmExistingDatabase = false)
    {
        Error = "";
        RequiresConfirmation = false;
        SchemaReady = false;
        foreach (var name in new[] { "Accounts", "Site" })
        {
            Step = name == "Accounts" ? "连接账号数据库" : "连接站点数据库";
            EnsureDatabase(config.GetConnectionString(name)!);
        }
        // FreeSql caches successful SyncStructure calls. A retry must inspect the actual tables again.
        Step = "准备数据库更新连接";
        db.Configure(config);
        if (!confirmExistingDatabase)
        {
            foreach (var database in new[] { db.Accounts, db.Site })
                if (!database.DbFirst.ExistsTable("schema_version") && database.DbFirst.GetTablesByDatabase().Count > 0)
                {
                    RequiresConfirmation = true;
                    return;
                }
        }
        db.InitializeSchema(step => Step = step);
        Refresh();
        if (!SchemaReady)
        {
            Step = "检查数据库更新结果";
            throw new InvalidOperationException(string.IsNullOrEmpty(Error) ? "数据库更新后版本检查未通过。请确认账号库和站点库的 schema_version 记录均为版本 4。" : Error);
        }
    }

    public void InitializeOnStartup()
    {
        Refresh();
        if (!Configured || SchemaReady) return;
        try
        {
            Initialize();
            if (RequiresConfirmation) logger.LogInformation("数据库已有数据表且没有版本记录，请在 /Setup 确认后继续更新。");
            else logger.LogInformation("数据库已自动初始化或升级，站点可以继续运行。");
        }
        catch (Exception ex) when (ex is MySqlException or ArgumentException or InvalidOperationException)
        {
            ReportFailure(ex);
        }
    }

    public void SaveConnections(string accounts, string site)
    {
        Error = "";
        Step = "连接账号数据库";
        EnsureDatabase(accounts);
        Step = "连接站点数据库";
        EnsureDatabase(site);
        Step = "保存数据库配置";
        var saved = ReadFile();
        saved["ConnectionStrings"] = new JsonObject { ["Accounts"] = accounts, ["Site"] = site };
        SaveFile(saved);
        SchemaReady = false;
        config["ConnectionStrings:Accounts"] = accounts;
        config["ConnectionStrings:Site"] = site;
        db.Configure(config);
        Refresh();
    }

    public string ReportFailure(Exception exception, params string?[] submittedSecrets)
    {
        var cause = exception;
        while (cause is not (MySqlException or IOException or UnauthorizedAccessException or ArgumentException) && cause.InnerException != null)
            cause = cause.InnerException;
        var guidance = cause switch
        {
            MySqlException { Number: 1045 } => "数据库拒绝登录，请检查账号、密码及该账号允许连接的来源地址。",
            MySqlException { Number: 1044 or 1142 or 1143 or 1227 } => "数据库账号缺少权限，请检查该库的访问、建表和修改表结构权限。",
            MySqlException { Number: 1049 } => "数据库不存在，自动创建也未成功。请先创建该库，或给数据库账号创建权限。",
            MySqlException { Number: 1054 or 1146 } => "数据库缺少所需的数据表或字段，可能有尚未完成的升级。",
            MySqlException { Number: 0 or 1042 or 2002 or 2003 } => "无法建立数据库连接，请检查地址、端口、容器网络和连接加密设置。",
            MySqlException => "数据库拒绝了这次操作，请根据下面的错误码和原因检查。",
            UnauthorizedAccessException or IOException => "无法读写配置文件，请检查 Docker 挂载目录是否只读，以及容器运行账号的写入权限。",
            ArgumentException => "数据库配置无效，请检查填写的连接信息。",
            _ => "数据库更新未完成，具体原因如下。"
        };
        var secrets = new List<string?> { config["Setup:Token"] };
        secrets.AddRange(submittedSecrets);
        foreach (var name in new[] { "Accounts", "Site" })
        {
            try { secrets.Add(new MySqlConnectionStringBuilder(config.GetConnectionString(name) ?? "").Password); }
            catch (ArgumentException) { /* Invalid connection text is not included in the diagnostic. */ }
        }
        var reason = cause.Message;
        foreach (var secret in secrets.Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderByDescending(s => s!.Length))
            reason = reason.Replace(secret!, "[已隐藏]", StringComparison.Ordinal);
        reason = Regex.Replace(reason, @"(?i)\b(password|pwd|token)\s*=\s*('[^']*'|""[^""]*""|[^;\s]*)", "$1=[已隐藏]");
        var code = cause is MySqlException mysql ? $"MySQL {mysql.Number}, SQLSTATE {mysql.SqlState}" : cause.GetType().Name;
        Error = UiText.Format("失败步骤：{0}。{1}（{2}）详细原因：{3}", UiText.T(Step), UiText.T(guidance), code, reason);
        logger.LogWarning("Setup failed at {Step} ({ErrorCode}): {Reason}\n{StackTrace}", Step, code, reason, cause.StackTrace);
        return Error;
    }

    private static void EnsureDatabase(string value)
    {
        var parsed = new MySqlConnectionStringBuilder(value);
        if (string.IsNullOrWhiteSpace(parsed.Server) || string.IsNullOrWhiteSpace(parsed.Database) || string.IsNullOrWhiteSpace(parsed.UserID))
            throw new ArgumentException("请填写数据库地址、数据库名称和账号。");
        using var connection = new MySqlConnection(value);
        try { connection.Open(); }
        catch (MySqlException ex) when (ex.Number == 1049)
        {
            var name = parsed.Database;
            parsed.Database = "";
            using var server = new MySqlConnection(parsed.ConnectionString);
            server.Open();
            using var create = new MySqlCommand($"CREATE DATABASE IF NOT EXISTS `{name.Replace("`", "``")}` CHARACTER SET utf8mb4", server);
            create.ExecuteNonQuery();
            connection.Open();
        }
    }

    private JsonObject ReadFile() => File.Exists(ConfigurationPath)
        ? JsonNode.Parse(File.ReadAllText(ConfigurationPath))!.AsObject() : new JsonObject();

    private void SaveFile(JsonObject document)
    {
        var temporary = ConfigurationPath + ".tmp";
        File.WriteAllText(temporary, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, ConfigurationPath, overwrite: true);
    }
}
