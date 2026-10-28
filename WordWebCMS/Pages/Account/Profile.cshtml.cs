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
    public async Task OnGetAsync()
    {
        Bio = await db.Accounts.Select<CmsUser>().Where(u => u.Id == AccountService.UserId(User)).FirstAsync(u => u.Bio) ?? "";
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        await db.Accounts.Update<CmsUser>().Set(u => u.Bio, (Bio ?? "").Trim())
            .Where(u => u.Id == AccountService.UserId(User)).ExecuteAffrowsAsync();
        return LocalRedirect(PublicUrls.Author(User.Identity!.Name!));
    }
}
