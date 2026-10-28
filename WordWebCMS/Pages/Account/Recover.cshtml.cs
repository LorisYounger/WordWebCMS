using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using WordWebCMS.Data;
using WordWebCMS.Models;
using WordWebCMS.Services;

namespace WordWebCMS.Pages.Account;

public class RecoverModel(CmsDb db, AccountService accounts, MailService mail) : PageModel
{
    [BindProperty, Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [BindProperty] public bool VerifyEmail { get; set; }
    public bool Sent { get; set; }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        if (!mail.Configured) { ModelState.AddModelError("", UiText.T("管理员尚未配置邮件服务。")); return Page(); }
        var email = Email.Trim().ToLowerInvariant();
        var account = await db.Accounts.Select<CmsUser>().Where(u => u.Email == email && !u.Disabled).FirstAsync();
        if (account != null && (VerifyEmail ? !account.EmailConfirmed : account.EmailConfirmed))
        {
            var purpose = VerifyEmail ? "verify" : "reset";
            var token = await accounts.CreateTokenAsync(account.Id, purpose);
            var settings = await db.Site.Select<SiteSettings>().FirstAsync();
            var link = $"{settings.BaseUrl}/Account/{(VerifyEmail ? "Verify" : "Reset")}?token={token}";
            await mail.SendAsync(email, VerifyEmail ? UiText.T("验证你的邮箱") : UiText.T("重置密码"), UiText.Format("请打开并确认：{0}。重置链接 1 小时有效，邮箱验证链接 24 小时有效。链接仅可使用一次。", link));
        }
        Sent = true;
        return Page();
    }
}
