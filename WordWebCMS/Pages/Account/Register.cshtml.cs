using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using WordWebCMS.Data;
using WordWebCMS.Models;
using WordWebCMS.Services;

namespace WordWebCMS.Pages.Account;

public class RegisterModel(CmsDb db, AccountService accounts, MailService mail) : PageModel
{
    [BindProperty, Required, StringLength(64)] public string UserName { get; set; } = "";
    [BindProperty, Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [BindProperty, Required, StringLength(128, MinimumLength = 12)] public string Password { get; set; } = "";
    public bool Sent { get; set; }
    public bool MailConfigured => mail.Configured;
    public RegistrationMode Registration { get; set; }
    public async Task OnGetAsync() => Registration = (await db.Site.Select<SiteSettings>().FirstAsync()).Registration;
    public async Task<IActionResult> OnPostAsync()
    {
        await OnGetAsync();
        if (Registration == RegistrationMode.Closed) { ModelState.AddModelError("", UiText.T("本站已关闭注册。")); return Page(); }
        if (!ModelState.IsValid) return Page();
        if (!mail.Configured) { ModelState.AddModelError("", UiText.T("管理员尚未配置邮件服务，暂不能注册。")); return Page(); }
        var account = await accounts.CreateAsync(UserName, Email, Password);
        await db.Site.Insert(new SiteUserRole { UserId = account.Id, Role = Registration == RegistrationMode.Approval ? SiteRole.Pending : SiteRole.Reader }).ExecuteAffrowsAsync();
        var token = await accounts.CreateTokenAsync(account.Id, "verify");
        var settings = await db.Site.Select<SiteSettings>().FirstAsync();
        await mail.SendAsync(account.Email, UiText.T("验证你的邮箱"), UiText.Format("请在 24 小时内打开并确认：{0}", $"{settings.BaseUrl}/Account/Verify?token={token}"));
        Sent = true;
        return Page();
    }
}
