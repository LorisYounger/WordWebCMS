namespace WordWebCMS.Models;

public class MailSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string Security { get; set; } = "StartTls";
    public string From { get; set; } = "";
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
}
