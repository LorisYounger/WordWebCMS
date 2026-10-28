using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WordWebCMS.Data;
using WordWebCMS.Models;

namespace WordWebCMS.Pages.Admin;
public class CommentsModel(CmsDb db) : PageModel
{
    public List<Comment> Comments { get; set; } = [];
    public Dictionary<long, string> Names { get; set; } = [];
    [BindProperty(SupportsGet = true)] public bool All { get; set; }
    [BindProperty(SupportsGet = true)] public int P { get; set; } = 1;
    public async Task OnGetAsync()
    {
        P = Math.Clamp(P, 1, 100000);
        Comments = await db.Site.Select<Comment>().WhereIf(!All, c => !c.Approved).OrderByDescending(c => c.Id).Page(P, 30).ToListAsync();
        var ids = Comments.Select(c => c.UserId).ToArray();
        Names = (await db.Accounts.Select<CmsUser>().Where(u => ids.Contains(u.Id)).ToListAsync(u => new { u.Id, u.UserName })).ToDictionary(u => u.Id, u => u.UserName);
    }
    public async Task<IActionResult> OnPostAsync(long id, string action)
    {
        if (action is "approve" or "hide")
            await db.Site.Update<Comment>().Set(c => c.Approved, action == "approve").Where(c => c.Id == id).ExecuteAffrowsAsync();
        else if (action == "delete") db.Site.Transaction(() =>
        {
            db.Site.Delete<CommentLike>().Where(l => l.CommentId == id).ExecuteAffrows();
            db.Site.Update<Comment>().Set(c => c.ParentId, (long?)null).Where(c => c.ParentId == id).ExecuteAffrows();
            db.Site.Delete<Comment>().Where(c => c.Id == id).ExecuteAffrows();
        });
        else return BadRequest();
        return RedirectToPage(new { All, P });
    }
}
