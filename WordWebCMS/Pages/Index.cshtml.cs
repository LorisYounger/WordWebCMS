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
        if (!ModelState.IsValid) return BadRequest();
        // Decode each original segment once: %252F is a literal "%2F", while %2F is a slash in a name.
        var rawPath = HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpRequestFeature>()?.RawTarget?.Split('?')[0] ?? Request.Path.Value!;
        string? categoryName = null, tagName = null, authorName = null;
        if (RouteData.Values.ContainsKey("filters"))
        {
            var parts = rawPath.Trim('/').Split('/').Select(Uri.UnescapeDataString).ToArray();
            var seen = new HashSet<string>();
            for (var i = 0; i < parts.Length; i += 2)
            {
                if (i + 1 >= parts.Length || !seen.Add(parts[i]) || string.IsNullOrWhiteSpace(parts[i + 1])) return NotFound();
                var value = parts[i + 1];
                switch (parts[i])
                {
                    case "category": categoryName = value; break;
                    case "tag": tagName = value; break;
                    case "author": authorName = value; break;
                    case "search": q = value; break;
                    case "page": if (!int.TryParse(value, out p) || p < 1 || p > 100000) return NotFound(); break;
                    case "archive":
                        if (!int.TryParse(value, out var y)) return NotFound();
                        year = y;
                        if (i + 2 < parts.Length && int.TryParse(parts[i + 2], out var m)) { month = m; i++; }
                        break;
                    default: return NotFound();
                }
            }
        }
        q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        p = Math.Clamp(p, 1, 100000);
        if (q?.Length > 200 || year is < 1 or > 9998 || month is < 1 or > 12 || month.HasValue && !year.HasValue) return BadRequest();
        var categories = await db.Site.Select<Category>().OrderBy(c => c.Name).ToListAsync();
        var selectedCategory = categoryName != null ? categories.FirstOrDefault(c => c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase)) : categories.FirstOrDefault(c => c.Id == category);
        if ((categoryName != null || category.HasValue) && selectedCategory == null) return NotFound();
        category = selectedCategory?.Id;
        var selectedTag = tagName != null ? await db.Site.Select<Tag>().Where(t => t.Name == tagName).FirstAsync() :
            tag.HasValue ? await db.Site.Select<Tag>().Where(t => t.Id == tag).FirstAsync() : null;
        if ((tagName != null || tag.HasValue) && selectedTag == null) return NotFound();
        tag = selectedTag?.Id;
        var author = authorName != null ? await db.Accounts.Select<CmsUser>().Where(u => u.UserName == authorName).FirstAsync() : null;
        if (authorName != null && (author == null || !await db.Site.Select<SiteUserRole>().Where(r => r.UserId == author.Id).AnyAsync())) return NotFound();
        var authorId = author?.Id;
        string ListUrl(int page) => PublicUrls.List(selectedCategory?.Name, selectedTag?.Name, year, month, q, author?.UserName, page);
        var canonicalPath = ListUrl(p);
        var extraQuery = Request.Query.Where(item => !new[] { "q", "category", "tag", "year", "month", "p" }.Contains(item.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(item => item.Key, item => (string?)item.Value.ToString());
        if (!string.Equals(Uri.UnescapeDataString(rawPath), Uri.UnescapeDataString(canonicalPath), StringComparison.Ordinal) ||
            Request.Query.Keys.Any(key => new[] { "q", "category", "tag", "year", "month", "p" }.Contains(key, StringComparer.OrdinalIgnoreCase)))
            return LocalRedirectPermanent(QueryHelpers.AddQueryString(canonicalPath, extraQuery));
        var query = db.Site.Select<Post, PostRevision>().InnerJoin((post, revision) => post.PublishedRevisionId == revision.Id)
            .Where((post, revision) => post.IsPublic && post.Kind == PostKind.Article)
            .WhereIf(!string.IsNullOrWhiteSpace(q), (post, revision) => revision.Title.Contains(q!) || revision.Markdown.Contains(q!) || revision.Translations.Contains(q!))
            .WhereIf(authorId.HasValue, (post, revision) => post.AuthorId == authorId)
            .WhereIf(category.HasValue, (post, revision) => revision.CategoryId == category)
            .WhereIf(tag.HasValue, (post, revision) => db.Site.Select<RevisionTag>().Where(rt => rt.RevisionId == revision.Id && rt.TagId == tag).Any());
        if (year.HasValue)
        {
            var from = new DateTime(year.Value, month ?? 1, 1, 0, 0, 0, DateTimeKind.Utc);
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
        var settings = (SiteSettings)HttpContext.Items["SiteSettings"]!;
        var localizedRows = rows.Select(r => new { r.Post, r.Revision, Text = ContentLanguages.Display(r.Revision, settings) }).ToArray();
        var data = new Dictionary<string, object?> {
            ["heading"] = author != null ? author.UserName : q != null ? UiText.T("搜索：") + q : category.HasValue ? categories.FirstOrDefault(c => c.Id == category)?.Name ?? UiText.T("分类") : tag.HasValue ? selectedTag!.Name : year.HasValue ? UiText.Format("{0} 年 {1} 归档", year, month?.ToString()) : UiText.T("最新文章"),
            ["title"] = author?.UserName ?? selectedCategory?.Name ?? selectedTag?.Name ?? q ?? (year.HasValue ? UiText.Format("{0} 年 {1} 归档", year, month?.ToString()) : UiText.T("最新文章")),
            ["profile"] = author == null ? null : new Dictionary<string, object?> { ["name"] = author.UserName, ["avatar"] = ShortLinks.Avatar(author.UserName), ["bio"] = author.Bio, ["own"] = author.Id == AccountService.UserId(User) },
            ["query"] = q ?? "", ["total"] = total, ["page"] = p,
            ["is_home"] = string.IsNullOrWhiteSpace(q) && !category.HasValue && !tag.HasValue && !year.HasValue && author == null && p == 1,
            ["previous"] = p > 1 ? QueryHelpers.AddQueryString(ListUrl(p - 1), extraQuery) : "",
            ["next"] = p * 10 < total ? QueryHelpers.AddQueryString(ListUrl(p + 1), extraQuery) : "",
            ["categories"] = categories.Select(c => new Dictionary<string, object?> { ["name"] = c.Name, ["url"] = PublicUrls.Category(c.Name) }).ToArray(),
            ["posts"] = localizedRows.Select(r => new Dictionary<string, object?> {
                ["id"] = r.Post.Id, ["title"] = r.Text.Title, ["summary"] = r.Text.Summary, ["pinned"] = r.Post.PinOrder > 0,
                ["category"] = categories.FirstOrDefault(c => c.Id == r.Revision.CategoryId)?.Name ?? "", ["category_url"] = PublicUrls.Category(categories.FirstOrDefault(c => c.Id == r.Revision.CategoryId)?.Name),
                ["url"] = "/" + r.Post.Slug, ["cover"] = r.Text.CoverUrl, ["date"] = r.Post.PublishedUtc?.ToString("yyyy-MM-dd"), ["date_utc"] = PublicUrls.Utc(r.Post.PublishedUtc),
                ["author_url"] = PublicUrls.Author(authors.GetValueOrDefault(r.Post.AuthorId, "user")),
                ["avatar"] = ShortLinks.Avatar(authors.GetValueOrDefault(r.Post.AuthorId, "user")), ["author"] = authors.GetValueOrDefault(r.Post.AuthorId, UiText.T("用户")), ["likes"] = likes.GetValueOrDefault(r.Post.Id), ["views"] = r.Post.Views
            }).ToArray()
        };
        return await themes.PageAsync(db, HttpContext, "list.liquid", data);
    }
}
