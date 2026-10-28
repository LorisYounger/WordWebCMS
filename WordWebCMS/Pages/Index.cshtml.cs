using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using WordWebCMS.Data;
using WordWebCMS.Models;
using WordWebCMS.Services;

namespace WordWebCMS.Pages;

public class IndexModel(CmsDb db, ThemeService themes) : PageModel
{
    public async Task<IActionResult> OnGetAsync(int p = 1, string? q = null, long? category = null, long? tag = null, int? year = null, int? month = null)
    {
        p = Math.Clamp(p, 1, 100000);
        if (q?.Length > 200 || year is < 1 or > 9998 || month is < 1 or > 12) return BadRequest();
        var query = db.Site.Select<Post, PostRevision>().InnerJoin((post, revision) => post.PublishedRevisionId == revision.Id)
            .Where((post, revision) => post.IsPublic && post.Kind == PostKind.Article)
            .WhereIf(!string.IsNullOrWhiteSpace(q), (post, revision) => revision.Title.Contains(q!) || revision.Markdown.Contains(q!) || revision.Translations.Contains(q!))
            .WhereIf(category.HasValue, (post, revision) => revision.CategoryId == category)
            .WhereIf(tag.HasValue, (post, revision) => db.Site.Select<RevisionTag>().Where(rt => rt.RevisionId == revision.Id && rt.TagId == tag).Any());
        if (year.HasValue)
        {
            var from = new DateTime(year.Value, month ?? 1, 1);
            var to = month.HasValue ? from.AddMonths(1) : from.AddYears(1);
            query.Where((post, revision) => post.PublishedUtc >= from && post.PublishedUtc < to);
        }
        var total = await query.CountAsync();
        var rows = await query.OrderByDescending((post, revision) => post.PinOrder).OrderByDescending((post, revision) => post.PublishedUtc)
            .OrderByDescending((post, revision) => post.Id).Page(p, 10).ToListAsync((post, revision) => new { Post = post, Revision = revision });
        var ids = rows.Select(r => r.Post.Id).ToArray();
        var authorIds = rows.Select(r => r.Post.AuthorId).ToArray();
        var authors = (await db.Accounts.Select<CmsUser>().Where(u => authorIds.Contains(u.Id)).ToListAsync(u => new { u.Id, u.UserName })).ToDictionary(u => u.Id, u => u.UserName);
        var likes = (await db.Site.Select<PostLike>().Where(l => ids.Contains(l.PostId)).GroupBy(l => l.PostId)
            .ToListAsync(g => new { Id = g.Key, Count = g.Count() })).ToDictionary(l => l.Id, l => l.Count);
        var categories = await db.Site.Select<Category>().OrderBy(c => c.Name).ToListAsync();
        var filters = new Dictionary<string, string?> { ["q"] = q, ["category"] = category?.ToString(), ["tag"] = tag?.ToString(), ["year"] = year?.ToString(), ["month"] = month?.ToString() };
        var settings = (SiteSettings)HttpContext.Items["SiteSettings"]!;
        var localizedRows = rows.Select(r => new { r.Post, r.Revision, Text = ContentLanguages.Display(r.Revision, settings) }).ToArray();
        var data = new Dictionary<string, object?> {
            ["heading"] = q != null ? UiText.T("搜索：") + q : category.HasValue ? categories.FirstOrDefault(c => c.Id == category)?.Name ?? UiText.T("分类") : tag.HasValue ? UiText.T("标签归档") : year.HasValue ? UiText.Format("{0} 年 {1} 归档", year, month?.ToString()) : UiText.T("最新文章"),
            ["query"] = q ?? "", ["total"] = total, ["page"] = p,
            ["is_home"] = string.IsNullOrWhiteSpace(q) && !category.HasValue && !tag.HasValue && !year.HasValue && p == 1,
            ["previous"] = p > 1 ? QueryHelpers.AddQueryString("/", new Dictionary<string, string?>(filters) { ["p"] = (p - 1).ToString() }) : "",
            ["next"] = p * 10 < total ? QueryHelpers.AddQueryString("/", new Dictionary<string, string?>(filters) { ["p"] = (p + 1).ToString() }) : "",
            ["categories"] = categories.Select(c => new Dictionary<string, object?> { ["name"] = c.Name, ["url"] = "/?category=" + c.Id }).ToArray(),
            ["posts"] = localizedRows.Select(r => new Dictionary<string, object?> {
                ["id"] = r.Post.Id, ["title"] = r.Text.Title, ["summary"] = r.Text.Summary, ["pinned"] = r.Post.PinOrder > 0,
                ["category"] = categories.FirstOrDefault(c => c.Id == r.Revision.CategoryId)?.Name ?? "", ["category_url"] = "/?category=" + r.Revision.CategoryId,
                ["url"] = "/" + r.Post.Slug, ["cover"] = r.Text.CoverUrl, ["date"] = r.Post.PublishedUtc?.ToString("yyyy-MM-dd"),
                ["avatar"] = ShortLinks.Avatar(authors.GetValueOrDefault(r.Post.AuthorId, "user")), ["author"] = authors.GetValueOrDefault(r.Post.AuthorId, UiText.T("用户")), ["likes"] = likes.GetValueOrDefault(r.Post.Id), ["views"] = r.Post.Views
            }).ToArray()
        };
        return await themes.PageAsync(db, HttpContext, "list.liquid", data);
    }
}
