using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    public string Error { get; private set; } = "";
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
        Error = "";
        if (!Configured) return;
        try
        {
            Installed = db.Site.DbFirst.ExistsTable("site_settings") && db.Site.Select<SiteSettings>().Any();
            SchemaReady = db.CheckSchema();
        }
        catch (Exception ex) when (ex is MySqlException or ArgumentException or InvalidOperationException)
        {
            Error = "无法连接数据库，请检查数据库地址、账号和密码后重试。";
            logger.LogWarning("Setup database check failed ({ErrorType}).", ex.GetType().Name);
        }
    }

    public void Initialize()
    {
        SchemaReady = false;
        foreach (var name in new[] { "Accounts", "Site" }) EnsureDatabase(config.GetConnectionString(name)!);
        // FreeSql caches successful SyncStructure calls. A retry must inspect the actual tables again.
        db.Configure(config);
        db.InitializeSchema();
        Refresh();
        if (!SchemaReady) throw new InvalidOperationException("数据库初始化后检查失败。");
    }

    public void InitializeOnStartup()
    {
        Refresh();
        if (!Configured || SchemaReady) return;
        try
        {
            Initialize();
            logger.LogInformation("数据库已自动初始化或升级，站点可以继续运行。");
        }
        catch (Exception ex) when (ex is MySqlException or ArgumentException or InvalidOperationException)
        {
            Error = "数据库准备失败，请检查连接信息、数据库权限和版本，然后在此页重试。";
            logger.LogWarning("Automatic database initialization failed ({ErrorType}). Open /Setup to retry.", ex.GetType().Name);
        }
    }

    public void SaveConnections(string accounts, string site)
    {
        foreach (var value in new[] { accounts, site }) EnsureDatabase(value);
        var saved = ReadFile();
        saved["ConnectionStrings"] = new JsonObject { ["Accounts"] = accounts, ["Site"] = site };
        SaveFile(saved);
        SchemaReady = false;
        config["ConnectionStrings:Accounts"] = accounts;
        config["ConnectionStrings:Site"] = site;
        db.Configure(config);
        Refresh();
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
