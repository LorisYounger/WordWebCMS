using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WordWebCMS.Data;
using WordWebCMS.Models;
using WordWebCMS.Services;

namespace WordWebCMS.Pages.Admin;
public class NavigationModel(CmsDb db) : PageModel
{
    public List<NavigationItem> Items { get; set; } = [];
    public async Task OnGetAsync() => Items = await db.Site.Select<NavigationItem>().OrderBy(n => n.SortOrder).OrderBy(n => n.Id).ToListAsync();
    public IActionResult OnPost(long id, string? text, string? url, int sortOrder, long? parentId)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 80 || string.IsNullOrWhiteSpace(url) || url.Length > 1000 || !MarkdownService.IsWebUrl(url, true))
            throw new InvalidDataException(UiText.T("导航文字和地址无效。"));
        db.Site.Transaction(() =>
        {
            db.Site.Select<SiteSettings>().Where(s => s.Id == 1).ForUpdate().First();
            if (parentId.HasValue && (parentId == id || !db.Site.Select<NavigationItem>().Where(n => n.Id == parentId && n.ParentId == null).Any() ||
                id != 0 && db.Site.Select<NavigationItem>().Where(n => n.ParentId == id).Any()))
                throw new InvalidDataException(UiText.T("导航最多两层；父项需为顶层导航，且不能形成循环。"));
            var item = new NavigationItem { Id = id, Text = text.Trim(), Url = url.Trim(), SortOrder = sortOrder, ParentId = parentId };
            if (id == 0) db.Site.Insert(item).ExecuteAffrows();
            else db.Site.Update<NavigationItem>().SetSource(item).ExecuteAffrows();
        });
        return RedirectToPage();
    }
    public IActionResult OnPostDelete(long id)
    {
        db.Site.Transaction(() =>
        {
            db.Site.Select<SiteSettings>().Where(s => s.Id == 1).ForUpdate().First();
            db.Site.Update<NavigationItem>().Set(n => n.ParentId, (long?)null).Where(n => n.ParentId == id).ExecuteAffrows();
            db.Site.Delete<NavigationItem>().Where(n => n.Id == id).ExecuteAffrows();
        });
        return RedirectToPage();
    }
}
