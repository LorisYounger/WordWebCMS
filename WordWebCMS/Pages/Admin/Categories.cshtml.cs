using WordWebCMS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WordWebCMS.Data;
using WordWebCMS.Models;

namespace WordWebCMS.Pages.Admin;
public class CategoriesModel(CmsDb db) : PageModel
{
    public List<Category> Categories { get; set; } = [];
    public async Task OnGetAsync() => Categories = await db.Site.Select<Category>().OrderBy(c => c.Name).ToListAsync();
    public async Task<IActionResult> OnPostAsync(long id, string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 80) return BadRequest(UiText.T("分类名称需为 1–80 字符。"));
        if (id == 0) await db.Site.Insert(new Category { Name = name.Trim() }).ExecuteAffrowsAsync();
        else await db.Site.Update<Category>().Set(c => c.Name, name.Trim()).Where(c => c.Id == id).ExecuteAffrowsAsync();
        return RedirectToPage();
    }
    public IActionResult OnPostDelete(long id)
    {
        db.Site.Transaction(() =>
        {
            db.Site.Select<Category>().Where(c => c.Id == id).ForUpdate().First();
            if (db.Site.Select<PostDraft>().Where(d => d.CategoryId == id).Any() ||
                db.Site.Select<PostRevision>().Where(r => r.CategoryId == id).Any())
                throw new InvalidDataException(UiText.T("分类仍被草稿或历史修订使用，不能删除；可以改名。"));
            db.Site.Delete<Category>().Where(c => c.Id == id).ExecuteAffrows();
        });
        return RedirectToPage();
    }
}
