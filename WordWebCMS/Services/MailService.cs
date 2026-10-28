using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace WordWebCMS.Services;

public class MailService(IConfiguration config)
{
    public bool Configured => !string.IsNullOrWhiteSpace(config["Mail:Host"]) && !string.IsNullOrWhiteSpace(config["Mail:From"]);

    public async Task SendAsync(string to, string subject, string text)
    {
        if (!Configured) throw new InvalidDataException(UiText.T("尚未配置 SMTP，请联系站点管理员。"));
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(config["Mail:From"]!));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = text };
        using var client = new SmtpClient();
        await client.ConnectAsync(config["Mail:Host"]!, config.GetValue("Mail:Port", 587),
            Enum.Parse<SecureSocketOptions>(config["Mail:Security"] ?? "StartTls", true));
        if (!string.IsNullOrWhiteSpace(config["Mail:User"]))
            await client.AuthenticateAsync(config["Mail:User"]!, config["Mail:Password"] ?? "");
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
