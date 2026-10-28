using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WordWebCMS.Data;
using WordWebCMS.Models;
using WordWebCMS.Services;

namespace WordWebCMS.Pages.Account;

[Authorize]
public class ProfileModel(CmsDb db) : PageModel
{
    [BindProperty, StringLength(1000)] public string Bio { get; set; } = "";
    [BindProperty, StringLength(1000)] public string AvatarUrl { get; set; } = "";
    public async Task OnGetAsync()
    {
        var account = await db.Accounts.Select<CmsUser>().Where(u => u.Id == AccountService.UserId(User)).FirstAsync();
        Bio = account.Bio;
        AvatarUrl = account.AvatarUrl;
    }
    public async Task<IActionResult> OnPostAsync()
    {
        AvatarUrl = (AvatarUrl ?? "").Trim();
        if (AvatarUrl.Length > 0 && !MarkdownService.IsWebUrl(AvatarUrl))
            ModelState.AddModelError(nameof(AvatarUrl), UiText.T("请填写站内图片路径或 HTTP(S) 图片链接。"));
        if (!ModelState.IsValid) return Page();
        await db.Accounts.Update<CmsUser>().Set(u => u.Bio, (Bio ?? "").Trim())
            .Set(u => u.AvatarUrl, AvatarUrl)
            .Where(u => u.Id == AccountService.UserId(User)).ExecuteAffrowsAsync();
        return LocalRedirect(PublicUrls.Author(User.Identity!.Name!));
    }
}
