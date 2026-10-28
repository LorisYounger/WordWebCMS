using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WordWebCMS.Data;
using WordWebCMS.Models;
using WordWebCMS.Services;

namespace WordWebCMS.Pages.Admin;
public class UsersModel(CmsDb db) : PageModel
{
    public List<SiteUserRole> Roles { get; set; } = [];
    public Dictionary<long, CmsUser> Accounts { get; set; } = [];
    public List<CmsUser> Pending { get; set; } = [];
    public long Total { get; set; }
    public int Pages => Math.Max(1, (int)Math.Ceiling(Total / 30d));
    [BindProperty(SupportsGet = true)] public int P { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    public static string RoleName(SiteRole role) => UiText.T(role switch {
        SiteRole.Removed => "已移除", SiteRole.Pending => "待审核", SiteRole.Reader => "注册用户",
        SiteRole.Contributor => "投稿者", SiteRole.Editor => "编辑", SiteRole.Administrator => "管理员",
        SiteRole.Banned => "已封禁", _ => "用户"
    });
    public async Task OnGetAsync()
    {
        Q = (Q ?? "").Trim();
        if (Q.Length > 254) Q = Q[..254];
        var query = db.Site.Select<SiteUserRole>();
        if (Enum.TryParse<SiteRole>(Status, out var filter) && Enum.IsDefined(filter)) query.Where(r => r.Role == filter);
        else query.Where(r => r.Role != SiteRole.Removed);
        if (Q.Length > 0 || Status is "unverified" or "Pending")
        {
            var matching = db.Accounts.Select<CmsUser>();
            if (Q.Length > 0) matching.Where(u => u.UserName.Contains(Q) || u.Email.Contains(Q));
            if (Status == "unverified") matching.Where(u => !u.EmailConfirmed);
            if (Status == "Pending") matching.Where(u => u.EmailConfirmed && !u.Disabled);
            var matches = await matching.ToListAsync(u => u.Id);
            query.Where(r => matches.Contains(r.UserId));
        }
        Total = await query.CountAsync();
        P = Math.Clamp(P, 1, Pages);
        Roles = await query.OrderBy(r => r.UserId).Page(P, 30).ToListAsync();
        var ids = Roles.Select(r => r.UserId).ToArray();
        Accounts = (await db.Accounts.Select<CmsUser>().Where(u => ids.Contains(u.Id)).ToListAsync()).ToDictionary(u => u.Id);
        var pendingIds = await db.Site.Select<SiteUserRole>().Where(r => r.Role == SiteRole.Pending).ToListAsync(r => r.UserId);
        Pending = await db.Accounts.Select<CmsUser>().Where(u => pendingIds.Contains(u.Id) && u.EmailConfirmed && !u.Disabled)
            .OrderBy(u => u.Id).Limit(100).ToListAsync();
    }
    public async Task<IActionResult> OnPostAsync(string? login, SiteRole role)
    {
        if (!ModelState.IsValid || !Enum.IsDefined(role) || role is SiteRole.Pending or SiteRole.Removed || string.IsNullOrWhiteSpace(login)) return BadRequest();
        login = login.Trim().ToLowerInvariant();
        var account = await db.Accounts.Select<CmsUser>().Where(u => u.UserName == login || u.Email == login).FirstAsync();
        if (account == null) throw new InvalidDataException(UiText.T("没有找到该共享账号。"));
        if (role >= SiteRole.Reader && (!account.EmailConfirmed || account.Disabled))
            throw new InvalidDataException(UiText.T("用户必须先验证邮箱且账号可用，才能批准注册或授予角色。"));
        ChangeRole(account.Id, role);
        TempData["UsersSaved"] = UiText.T("本站角色已更新。");
        return RedirectToPage(new { Q, Status, P });
    }
    public async Task<IActionResult> OnPostRemoveAsync(long userId)
    {
        if (!ModelState.IsValid || userId <= 0) return BadRequest();
        if (!await db.Site.Select<SiteUserRole>().Where(r => r.UserId == userId).AnyAsync()) return NotFound();
        // Keep a site-local tombstone so open registration cannot silently re-add the account.
        ChangeRole(userId, SiteRole.Removed);
        TempData["UsersSaved"] = UiText.T("用户已从本站移除，已发表内容及其他站点账号不受影响。");
        return RedirectToPage(new { Q, Status, P });
    }
    private void ChangeRole(long userId, SiteRole role)
    {
        if (userId == AccountService.UserId(User)) throw new InvalidDataException(UiText.T("不能在此修改自己的管理员角色，请由另一位管理员操作。"));
        db.Site.Transaction(() =>
        {
            db.Site.Select<SiteSettings>().Where(s => s.Id == 1).ForUpdate().First();
            var currentRole = db.Site.Select<SiteUserRole>().Where(r => r.UserId == userId).First();
            if (currentRole?.Role == SiteRole.Administrator && role != SiteRole.Administrator &&
                db.Site.Select<SiteUserRole>().Where(r => r.Role == SiteRole.Administrator).Count() <= 1)
                throw new InvalidDataException(UiText.T("至少需要保留一位站点管理员。"));
            db.Site.InsertOrUpdate<SiteUserRole>().SetSource(new SiteUserRole { UserId = userId, Role = role }).ExecuteAffrows();
        });
    }
}
