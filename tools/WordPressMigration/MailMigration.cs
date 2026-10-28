using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using WordWebCMS.Models;
using WordWebCMS.Services;

static class MailMigration
{
    public static async Task Run(MigrationConfig config)
    {
        if (config.DeploymentPath != @"F:\Downloads\lbcp.exlb.net\202608111453\wwcms_lbnet") throw new InvalidOperationException("Unexpected deployment path.");
        using var connection = new MySqlConnection(config.SourceConnection);
        await connection.OpenAsync();
        await new MySqlCommand("SET SESSION TRANSACTION READ ONLY", connection).ExecuteNonQueryAsync();
        async Task<string> Option(string name) {
            using var command = new MySqlCommand("SELECT option_value FROM wp_options WHERE option_name=@name", connection);
            command.Parameters.AddWithValue("@name", name);
            return (await command.ExecuteScalarAsync())?.ToString() ?? "";
        }
        var raw = await Option("wp_mail_smtp");
        string Field(string key) {
            var match = Regex.Match(raw, "s:" + key.Length + ":\"" + key + "\";(?:s:[0-9]+:\"(.*?)\"|i:([0-9]+));", RegexOptions.Singleline);
            return match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        }
        if (Field("mailer") != "smtp") throw new InvalidOperationException("Only an ordinary SMTP profile was reviewed.");
        var encoded = Field("pass");
        var packed = Convert.FromBase64String(encoded);
        var key = Convert.FromBase64String(await Option("wp_mail_smtp_mail_key"));
        // WP Mail SMTP Crypto.php uses libsodium secretbox: nonce(24) || MAC(16) || ciphertext.
        // Verify the authentication tag before accepting any decrypted credentials.
        if (key.Length != 32 || packed.Length < 40) throw new InvalidDataException("Unrecognized SMTP encryption; no settings written.");
        var engine = new XSalsa20Engine();
        engine.Init(false, new ParametersWithIV(new KeyParameter(key), packed[..24]));
        var polyKey = new byte[32];
        engine.ProcessBytes(new byte[32], 0, 32, polyKey, 0);
        var mac = new Poly1305();
        mac.Init(new KeyParameter(polyKey));
        mac.BlockUpdate(packed, 40, packed.Length - 40);
        var tag = new byte[16];
        mac.DoFinal(tag, 0);
        if (!CryptographicOperations.FixedTimeEquals(tag, packed.AsSpan(24, 16))) throw new CryptographicException("SMTP secretbox authentication failed; no settings written.");
        var plain = new byte[packed.Length - 40];
        engine.ProcessBytes(packed, 40, plain.Length, plain, 0);
        var settings = new MailSettings { Host = Field("host"), Port = int.Parse(Field("port")), From = Field("from_email"), User = Field("user"), Password = Encoding.UTF8.GetString(plain),
            Security = Field("encryption") switch { "ssl" => "SslOnConnect", "tls" => "StartTls", _ => throw new InvalidDataException("Unreviewed SMTP encryption mode.") } };
        CryptographicOperations.ZeroMemory(key);
        CryptographicOperations.ZeroMemory(polyKey);
        CryptographicOperations.ZeroMemory(plain);
        var storage = new StoragePaths(Path.Combine(config.DeploymentPath, "App_Data"));
        if (File.Exists(Path.Combine(storage.Root, "mail.json"))) throw new InvalidOperationException("Existing mail settings will not be overwritten.");
        var protection = DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(storage.Root, "keys")), builder => builder.SetApplicationName("WordWebCMS-lbnet"));
        var service = new MailService(new ConfigurationBuilder().Build(), storage, protection);
        service.SaveSettings(settings, false);
        var loaded = service.ReadSettings();
        var authenticated = false;
        var error = "";
        try {
            using var smtp = new SmtpClient { Timeout = 15000 };
            await smtp.ConnectAsync(loaded.Host, loaded.Port, Enum.Parse<SecureSocketOptions>(loaded.Security));
            await smtp.AuthenticateAsync(loaded.User, loaded.Password);
            authenticated = smtp.IsAuthenticated;
            await smtp.DisconnectAsync(true);
        }
        catch (Exception ex) { error = ex.GetType().Name; }
        var result = new { ConfigurationMigrated = true, SecretboxMacVerified = true, TargetPasswordEncrypted = true,
            Authenticated = authenticated, ErrorType = error, MessagesSent = 0 };
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(config.OutputPath, "mail-verification.json"), json);
        File.WriteAllText(Path.Combine(config.DeploymentPath, "Migration-private", "mail-verification.json"), json);
        Console.WriteLine(json);
    }
}
