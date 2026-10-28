using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WordWebCMS.Data;
using WordWebCMS.Models;
using WordWebCMS.Services;

namespace WordWebCMS.Pages.Admin;
public class UsersModel(CmsDb db) : PageModel
{
    public List<SiteUserRole> Roles { get; set; } = [];
    public Dictionary<long, string> Names { get; set; } = [];
    public List<CmsUser> Pending { get; set; } = [];
    [BindProperty(SupportsGet = true)] public int P { get; set; } = 1;
    public async Task OnGetAsync()
    {
        P = Math.Clamp(P, 1, 100000);
        Roles = await db.Site.Select<SiteUserRole>().OrderBy(r => r.UserId).Page(P, 30).ToListAsync();
        var ids = Roles.Select(r => r.UserId).ToArray();
        Names = (await db.Accounts.Select<CmsUser>().Where(u => ids.Contains(u.Id)).ToListAsync(u => new { u.Id, u.UserName })).ToDictionary(u => u.Id, u => u.UserName);
        var pendingIds = await db.Site.Select<SiteUserRole>().Where(r => r.Role == SiteRole.Pending).OrderBy(r => r.UserId).Limit(100).ToListAsync(r => r.UserId);
        Pending = await db.Accounts.Select<CmsUser>().Where(u => pendingIds.Contains(u.Id)).OrderBy(u => u.Id).ToListAsync();
    }
    public async Task<IActionResult> OnPostAsync(string? login, SiteRole role)
    {
        if (!ModelState.IsValid || !Enum.IsDefined(role) || role == SiteRole.Pending || string.IsNullOrWhiteSpace(login)) return BadRequest();
        login = login.Trim().ToLowerInvariant();
        var account = await db.Accounts.Select<CmsUser>().Where(u => u.UserName == login || u.Email == login).FirstAsync();
        if (account == null) throw new InvalidDataException(UiText.T("没有找到该共享账号。"));
        if (account.Id == AccountService.UserId(User)) throw new InvalidDataException(UiText.T("不能在此修改自己的管理员角色，请由另一位管理员操作。"));
        db.Site.Transaction(() =>
        {
            db.Site.Select<SiteSettings>().Where(s => s.Id == 1).ForUpdate().First();
            var currentRole = db.Site.Select<SiteUserRole>().Where(r => r.UserId == account.Id).First();
            if (currentRole?.Role == SiteRole.Administrator && role != SiteRole.Administrator &&
                db.Site.Select<SiteUserRole>().Where(r => r.Role == SiteRole.Administrator).Count() <= 1)
                throw new InvalidDataException(UiText.T("至少需要保留一位站点管理员。"));
            db.Site.InsertOrUpdate<SiteUserRole>().SetSource(new SiteUserRole { UserId = account.Id, Role = role }).ExecuteAffrows();
        });
        return RedirectToPage();
    }
}
