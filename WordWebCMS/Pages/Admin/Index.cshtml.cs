using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WordWebCMS.Data;
using WordWebCMS.Models;
using WordWebCMS.Services;

namespace WordWebCMS.Pages.Admin;

public class IndexModel(CmsDb db, ContentService content) : PageModel
{
    public List<Post> Posts { get; set; } = [];
    public Dictionary<long, PostDraft> Drafts { get; set; } = [];
    public HashSet<long> Pending { get; set; } = [];
    public Dictionary<long, string> Authors { get; set; } = [];
    public Dictionary<long, string> Categories { get; set; } = [];
    public long Total { get; set; }
    public long Pages => Math.Max(1, (Total + 19) / 20);
    [BindProperty(SupportsGet = true)] public int P { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public PostKind? Kind { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    public async Task OnGetAsync()
    {
        P = Math.Clamp(P, 1, 100000);
        long userId = AccountService.UserId(User);
        var query = db.Site.Select<Post>().WhereIf(!AccountService.CanEditAll(User), p => p.AuthorId == userId)
            .WhereIf(Kind.HasValue, p => p.Kind == Kind)
            .WhereIf(Status == "published", p => p.IsPublic)
            .WhereIf(Status == "draft", p => p.PublishedRevisionId == null)
            .WhereIf(Status == "hidden", p => !p.IsPublic && p.PublishedRevisionId != null)
            .WhereIf(Status == "pending", p => db.Site.Select<PostRevision>().Where(r => r.PostId == p.Id && r.State == RevisionState.Pending).Any())
            .WhereIf(!string.IsNullOrWhiteSpace(Q), p => db.Site.Select<PostDraft>().Where(d => d.PostId == p.Id && d.Title.Contains(Q!)).Any());
        Total = await query.CountAsync();
        P = (int)Math.Min(P, Pages);
        Posts = await query.OrderByDescending(p => p.Id).Page(P, 20).ToListAsync();
        var ids = Posts.Select(p => p.Id).ToArray();
        Drafts = (await db.Site.Select<PostDraft>().Where(d => ids.Contains(d.PostId)).ToListAsync()).ToDictionary(d => d.PostId);
        Pending = (await db.Site.Select<PostRevision>().Where(r => ids.Contains(r.PostId) && r.State == RevisionState.Pending).ToListAsync(r => r.PostId)).ToHashSet();
        var authorIds = Posts.Select(p => p.AuthorId).Distinct().ToArray();
        Authors = (await db.Accounts.Select<CmsUser>().Where(u => authorIds.Contains(u.Id)).ToListAsync()).ToDictionary(u => u.Id, u => u.UserName);
        Categories = (await db.Site.Select<Category>().ToListAsync()).ToDictionary(c => c.Id, c => c.Name);
    }
    public IActionResult OnPostCreate(PostKind kind) => RedirectToPage("Edit", new { id = content.Create(kind, User) });
    public IActionResult OnPostDelete(long id) { content.Delete(id, User); return RedirectToPage(); }
    public IActionResult OnPostVisibility(long id, bool visible, int pinOrder)
    {
        if (!AccountService.CanEditAll(User)) return Forbid();
        db.Site.Transaction(() =>
        {
            var post = content.RequireEditable(id, User, true);
            if (post.PublishedRevisionId == null) throw new InvalidDataException(UiText.T("请先发布一个修订。"));
            db.Site.Update<Post>().Set(p => p.IsPublic, visible).Set(p => p.PinOrder, pinOrder).Where(p => p.Id == id).ExecuteAffrows();
        });
        return RedirectToPage();
    }
}
