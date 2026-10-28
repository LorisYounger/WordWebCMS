using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.AspNetCore.DataProtection;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using WordWebCMS.Models;

namespace WordWebCMS.Services;

public class MailService(IConfiguration config, StoragePaths paths, IDataProtectionProvider protection)
{
    private readonly object sync = new();
    private readonly IDataProtector protector = protection.CreateProtector("WordWebCMS.Mail.Password.v1");
    private readonly string settingsPath = Path.Combine(paths.Root, "mail.json");

    public MailSettings ReadSettings()
    {
        lock (sync)
        {
            if (!File.Exists(settingsPath)) return config.GetSection("Mail").Get<MailSettings>() ?? new();
            var settings = JsonSerializer.Deserialize<MailSettings>(File.ReadAllText(settingsPath))
                ?? throw new InvalidDataException("Invalid mail settings.");
            if (settings.Password.Length > 0) settings.Password = protector.Unprotect(settings.Password);
            return settings;
        }
    }

    public bool Configured
    {
        get
        {
            var settings = ReadSettings();
            return !string.IsNullOrWhiteSpace(settings.Host) && !string.IsNullOrWhiteSpace(settings.From);
        }
    }

    public void SaveSettings(MailSettings settings, bool clearPassword)
    {
        settings.Host = (settings.Host ?? "").Trim();
        settings.From = (settings.From ?? "").Trim();
        settings.User = (settings.User ?? "").Trim();
        settings.Password ??= "";
        if (settings.Host.Length > 253 || settings.Host.Any(char.IsWhiteSpace) ||
            settings.Host.Length > 0 && Uri.CheckHostName(settings.Host) == UriHostNameType.Unknown ||
            settings.Port is < 1 or > 65535 || settings.Security is not ("StartTls" or "SslOnConnect" or "None") ||
            settings.From.Length > 254 || settings.User.Length > 320 || settings.Password.Length > 4096 ||
            (settings.Host.Length == 0) != (settings.From.Length == 0) ||
            settings.From.Length > 0 && (!new EmailAddressAttribute().IsValid(settings.From) ||
                !MailboxAddress.TryParse(settings.From, out _) || settings.From.IndexOfAny(['\r', '\n']) >= 0))
            throw new InvalidDataException(UiText.T("请检查 SMTP 地址、端口、加密方式和发件地址。"));
        lock (sync)
        {
            var password = clearPassword ? "" : settings.Password.Length > 0 ? settings.Password : ReadSettings().Password;
            settings.Password = password.Length > 0 ? protector.Protect(password) : "";
            try
            {
                File.WriteAllText(settingsPath + ".tmp", JsonSerializer.Serialize(settings));
                File.Move(settingsPath + ".tmp", settingsPath, overwrite: true);
            }
            finally { settings.Password = ""; }
        }
    }

    public async Task SendAsync(string to, string subject, string text)
    {
        var settings = ReadSettings();
        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.From))
            throw new InvalidDataException(UiText.T("尚未配置 SMTP，请联系站点管理员。"));
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(settings.From));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = text };
        using var client = new SmtpClient();
        await client.ConnectAsync(settings.Host, settings.Port, Enum.Parse<SecureSocketOptions>(settings.Security, true));
        if (!string.IsNullOrWhiteSpace(settings.User))
            await client.AuthenticateAsync(settings.User, settings.Password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
