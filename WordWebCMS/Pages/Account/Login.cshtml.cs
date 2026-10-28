using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using WordWebCMS.Services;

namespace WordWebCMS.Pages.Account;

public class LoginModel(AccountService accounts) : PageModel
{
    [BindProperty, Required, StringLength(254)] public string Login { get; set; } = "";
    [BindProperty, Required, StringLength(128)] public string Password { get; set; } = "";
    [BindProperty] public bool Remember { get; set; }
    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        if (!ModelState.IsValid) return Page();
        var account = await accounts.CheckPasswordAsync(Login, Password);
        if (account == null || !account.EmailConfirmed)
        { ModelState.AddModelError("", UiText.T("用户名或密码错误，或邮箱尚未验证。")); return Page(); }
        await accounts.SignInAsync(HttpContext, account, Remember);
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }
}
