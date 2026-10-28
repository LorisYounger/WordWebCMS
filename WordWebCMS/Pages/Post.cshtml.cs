using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WordWebCMS.Data;
using WordWebCMS.Models;
using WordWebCMS.Services;

namespace WordWebCMS.Pages;

public class PostModel(CmsDb db, ThemeService themes, MarkdownService markdown) : PageModel
{
    public async Task<IActionResult> OnGetAsync(string slug, int cp = 1)
    {
        var post = await db.Site.Select<Post>().Where(p => p.Slug == slug && p.IsPublic && p.PublishedRevisionId != null).FirstAsync();
        if (post == null) return NotFound();
        if (!ShortLinks.IsValid(post.Slug)) return NotFound();
        if (Request.Path != "/" + post.Slug) return LocalRedirectPermanent("/" + post.Slug + Request.QueryString);
        var revision = await db.Site.Select<PostRevision>().Where(r => r.Id == post.PublishedRevisionId).FirstAsync();
        await db.Site.Update<Post>().Set(p => p.Views + 1).Where(p => p.Id == post.Id).ExecuteAffrowsAsync();
        var settings = (SiteSettings)HttpContext.Items["SiteSettings"]!;
        var rendered = ContentLanguages.Display(revision, settings);
        if (post.Kind == PostKind.Page)
            return await themes.PageAsync(db, HttpContext, "page.liquid", new Dictionary<string, object?> {
                ["title"] = rendered.Title, ["description"] = rendered.Summary, ["cover"] = rendered.CoverUrl, ["is_page"] = true,
                ["canonical"] = settings.BaseUrl + "/" + post.Slug,
                ["post"] = new Dictionary<string, object?> { ["title"] = rendered.Title, ["summary"] = rendered.Summary,
                    ["cover"] = rendered.CoverUrl, ["html"] = markdown.Render(rendered.Markdown), ["url"] = "/" + post.Slug }
            });
        var userId = AccountService.UserId(User);
        cp = Math.Clamp(cp, 1, 100000);
        var commentsQuery = db.Site.Select<Comment>().Where(c => c.PostId == post.Id && c.Approved);
        var count = await commentsQuery.CountAsync();
        var comments = await commentsQuery.OrderBy(c => c.Id).Page(cp, 30).ToListAsync();
        var ids = comments.Select(c => c.Id).ToArray();
        var users = comments.Select(c => c.UserId).Append(post.AuthorId).ToArray();
        var names = (await db.Accounts.Select<CmsUser>().Where(u => users.Contains(u.Id)).ToListAsync(u => new { u.Id, u.UserName })).ToDictionary(u => u.Id, u => u.UserName);
        var commentLikes = (await db.Site.Select<CommentLike>().Where(l => ids.Contains(l.CommentId)).GroupBy(l => l.CommentId)
            .ToListAsync(g => new { Id = g.Key, Count = g.Count() })).ToDictionary(l => l.Id, l => l.Count);
        var likedComments = (await db.Site.Select<CommentLike>().Where(l => ids.Contains(l.CommentId) && l.UserId == userId).ToListAsync(l => l.CommentId)).ToHashSet();
        var tags = await db.Site.Select<Tag, RevisionTag>().InnerJoin((t, rt) => t.Id == rt.TagId)
            .Where((t, rt) => rt.RevisionId == revision.Id).ToListAsync((t, rt) => new { t.Id, t.Name });
        var category = revision.CategoryId.HasValue ? await db.Site.Select<Category>().Where(c => c.Id == revision.CategoryId).FirstAsync() : null;
        return await themes.PageAsync(db, HttpContext, "post.liquid", new Dictionary<string, object?> {
            ["title"] = rendered.Title, ["description"] = rendered.Summary, ["cover"] = rendered.CoverUrl,
            ["post"] = new Dictionary<string, object?> { ["title"] = rendered.Title, ["summary"] = rendered.Summary, ["html"] = markdown.Render(rendered.Markdown),
                ["url"] = "/" + post.Slug, ["cover"] = rendered.CoverUrl, ["avatar"] = ShortLinks.Avatar(names.GetValueOrDefault(post.AuthorId, "user")), ["author"] = names.GetValueOrDefault(post.AuthorId, UiText.T("用户")),
                ["date"] = post.PublishedUtc?.ToString("yyyy-MM-dd"), ["archive_url"] = $"/?year={post.PublishedUtc?.Year}&month={post.PublishedUtc?.Month}",
                ["views"] = post.Views + 1, ["likes"] = await db.Site.Select<PostLike>().Where(l => l.PostId == post.Id).CountAsync(),
                ["liked"] = userId != 0 && await db.Site.Select<PostLike>().Where(l => l.PostId == post.Id && l.UserId == userId).AnyAsync(),
                ["category"] = category?.Name ?? "", ["category_url"] = "/?category=" + category?.Id },
            ["tags"] = tags.Select(t => new Dictionary<string, object?> { ["name"] = t.Name, ["url"] = "/?tag=" + t.Id }).ToArray(),
            ["comments_open"] = settings.CommentsEnabled && revision.AllowComments, ["moderated"] = settings.ModerateComments,
            ["comments"] = comments.Select(c => new Dictionary<string, object?> { ["id"] = c.Id, ["parent_id"] = c.ParentId,
                ["avatar"] = ShortLinks.Avatar(names.GetValueOrDefault(c.UserId, "user")), ["author"] = names.GetValueOrDefault(c.UserId, UiText.T("用户")), ["html"] = markdown.Render(c.Markdown, true),
                ["date"] = c.CreatedUtc.ToString("yyyy-MM-dd HH:mm"), ["likes"] = commentLikes.GetValueOrDefault(c.Id), ["liked"] = likedComments.Contains(c.Id) }).ToArray(),
            ["comment_previous"] = cp > 1 ? $"/{post.Slug}?cp={cp - 1}#comments" : "",
            ["comment_next"] = cp * 30 < count ? $"/{post.Slug}?cp={cp + 1}#comments" : "",
            ["notice"] = TempData["Notice"]?.ToString() ?? ""
        });
    }

    public IActionResult OnPostComment(string slug, string? body, long? parentId)
    {
        if (User.Identity?.IsAuthenticated != true) return Challenge();
        if (string.IsNullOrWhiteSpace(body) || body.Length > 10000) return BadRequest(UiText.T("评论需为 1–10000 字符。"));
        db.Site.Transaction(() =>
        {
            var post = db.Site.Select<Post>().Where(p => p.Slug == slug && p.IsPublic && p.PublishedRevisionId != null).ForUpdate().First();
            if (post == null) throw new InvalidDataException(UiText.T("文章不存在或未公开。"));
            var revision = db.Site.Select<PostRevision>().Where(r => r.Id == post.PublishedRevisionId).First();
            var settings = db.Site.Select<SiteSettings>().First();
            if (post.Kind == PostKind.Page || !settings.CommentsEnabled || !revision.AllowComments) throw new InvalidDataException(UiText.T("评论已关闭。"));
            if (parentId.HasValue && !db.Site.Select<Comment>().Where(c => c.Id == parentId && c.PostId == post.Id && c.Approved).Any())
                throw new InvalidDataException(UiText.T("回复目标不存在或未通过审核。"));
            db.Site.Insert(new Comment { PostId = post.Id, UserId = AccountService.UserId(User), ParentId = parentId,
                Markdown = body.Trim(), Approved = !settings.ModerateComments }).ExecuteAffrows();
            TempData["Notice"] = settings.ModerateComments ? UiText.T("评论已提交，等待审核。") : UiText.T("评论已发布。");
        });
        return LocalRedirect("/" + slug + "#comments");
    }

    public IActionResult OnPostLike(string slug, long? commentId, bool liked)
    {
        if (User.Identity?.IsAuthenticated != true) return Challenge();
        var userId = AccountService.UserId(User);
        db.Site.Transaction(() =>
        {
            var post = db.Site.Select<Post>().Where(p => p.Slug == slug && p.IsPublic && p.PublishedRevisionId != null).ForUpdate().First();
            if (post == null) throw new InvalidDataException(UiText.T("文章不存在或未公开。"));
            if (post.Kind == PostKind.Page) throw new InvalidDataException(UiText.T("页面不支持点赞或评论。"));
            if (commentId.HasValue)
            {
                if (!db.Site.Select<Comment>().Where(c => c.Id == commentId && c.PostId == post.Id && c.Approved).Any())
                    throw new InvalidDataException(UiText.T("评论不存在或未审核。"));
                if (liked) db.Site.InsertOrUpdate<CommentLike>().SetSource(new CommentLike { CommentId = commentId.Value, UserId = userId }).IfExistsDoNothing().ExecuteAffrows();
                else db.Site.Delete<CommentLike>().Where(l => l.CommentId == commentId && l.UserId == userId).ExecuteAffrows();
            }
            else
            {
                if (liked) db.Site.InsertOrUpdate<PostLike>().SetSource(new PostLike { PostId = post.Id, UserId = userId }).IfExistsDoNothing().ExecuteAffrows();
                else db.Site.Delete<PostLike>().Where(l => l.PostId == post.Id && l.UserId == userId).ExecuteAffrows();
            }
        });
        return LocalRedirect("/" + slug + (commentId.HasValue ? "#comment-" + commentId : ""));
    }
}
