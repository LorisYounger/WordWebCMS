using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MySqlConnector;
using WordWebCMS.Data;
using WordWebCMS.Models;

namespace WordWebCMS.Services;

public sealed class SetupService(IConfiguration config, CmsDb db, StoragePaths storage, ILogger<SetupService> logger)
{
    public SemaphoreSlim Gate { get; } = new(1, 1);
    public bool Configured => !string.IsNullOrWhiteSpace(config.GetConnectionString("Accounts")) &&
        !string.IsNullOrWhiteSpace(config.GetConnectionString("Site"));
    public bool SchemaReady { get; private set; }
    public bool Installed { get; private set; }
    public string Error { get; private set; } = "";
    public string ConfigurationPath => Path.Combine(storage.Root, "setup.json");

    public void EnsureToken()
    {
        if (!string.IsNullOrWhiteSpace(config["Setup:Token"])) return;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var saved = File.Exists(ConfigurationPath) ? JsonNode.Parse(File.ReadAllText(ConfigurationPath))!.AsObject() : new JsonObject();
        saved["Setup"] = new JsonObject { ["Token"] = token };
        SaveFile(saved);
        config["Setup:Token"] = token;
        logger.LogInformation("安装令牌已生成，请从服务器文件 {Path} 读取 Setup:Token。", ConfigurationPath);
    }

    public bool VerifyToken(string token) => !string.IsNullOrWhiteSpace(config["Setup:Token"]) &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(config["Setup:Token"]!));

    public void Refresh()
    {
        SchemaReady = false;
        Installed = false;
        Error = "";
        if (!Configured) return;
        try
        {
            // The installation lock also applies to older schema versions.
            Installed = db.Site.DbFirst.ExistsTable("site_settings") && db.Site.Select<SiteSettings>().Any();
            SchemaReady = db.CheckSchema();
        }
        catch (Exception ex) when (ex is MySqlException or ArgumentException or InvalidOperationException)
        {
            Error = "无法检查数据库，请确认连接字符串、数据库是否已创建及账号权限。";
            logger.LogWarning("Setup database check failed ({ErrorType}).", ex.GetType().Name);
        }
    }

    public void SaveConnections(string accounts, string site)
    {
        // Validate both databases before persisting or changing the running connections.
        foreach (var value in new[] { accounts, site })
        {
            var parsed = new MySqlConnectionStringBuilder(value);
            if (string.IsNullOrWhiteSpace(parsed.Server) || string.IsNullOrWhiteSpace(parsed.Database) || string.IsNullOrWhiteSpace(parsed.UserID))
                throw new ArgumentException("连接字符串必须包含 Server、Database 和 User ID。");
            using var connection = new MySqlConnection(value);
            connection.Open();
        }
        var saved = File.Exists(ConfigurationPath) ? JsonNode.Parse(File.ReadAllText(ConfigurationPath))!.AsObject() : new JsonObject();
        saved["ConnectionStrings"] = new JsonObject { ["Accounts"] = accounts, ["Site"] = site };
        SaveFile(saved);
        config["ConnectionStrings:Accounts"] = accounts;
        config["ConnectionStrings:Site"] = site;
        db.Configure(config);
        Refresh();
    }

    private void SaveFile(JsonObject document)
    {
        var temporary = ConfigurationPath + ".tmp";
        File.WriteAllText(temporary, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, ConfigurationPath, overwrite: true);
    }
}
