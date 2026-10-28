using System.Security.Claims;
using WordWebCMS.Data;
using WordWebCMS.Models;

namespace WordWebCMS.Services;

public class ContentService(CmsDb db)
{
    public Post RequireEditable(long id, ClaimsPrincipal user, bool forUpdate = false)
    {
        var query = db.Site.Select<Post>().Where(p => p.Id == id);
        var post = (forUpdate ? query.ForUpdate() : query).First();
        if (post == null) throw new InvalidDataException(UiText.T("内容不存在。"));
        if (!AccountService.CanEditAll(user) && !(user.IsInRole("Contributor") && post.AuthorId == AccountService.UserId(user)))
            throw new UnauthorizedAccessException(UiText.T("你没有此内容的编辑权限。"));
        return post;
    }

    public long Create(PostKind kind, ClaimsPrincipal user)
    {
        if (!Enum.IsDefined(kind)) throw new InvalidDataException(UiText.T("内容类型无效。"));
        var post = new Post { Kind = kind, AuthorId = AccountService.UserId(user) };
        db.Site.Transaction(() =>
        {
            post.Id = db.Site.Insert(post).ExecuteIdentity();
            post.Slug = (kind == PostKind.Page ? "new-page-" : "new-article-") + post.Id;
            while (db.Site.Select<Post>().Where(p => p.Slug == post.Slug && p.Id != post.Id).Any()) post.Slug += "-new";
            db.Site.Update<Post>().SetSource(post).ExecuteAffrows();
            db.Site.Insert(new PostDraft { PostId = post.Id, Slug = "", AllowComments = kind == PostKind.Article }).ExecuteAffrows();
        });
        return post.Id;
    }

    public long Save(long id, PostDraft input, ClaimsPrincipal user, string action)
    {
        if (action is not ("autosave" or "save" or "submit" or "publish")) throw new InvalidDataException(UiText.T("未知操作。"));
        if (action == "publish" && !AccountService.CanEditAll(user)) throw new UnauthorizedAccessException(UiText.T("需要发布权限。"));
        input.Title = (input.Title ?? "").Trim();
        input.Slug = (input.Slug ?? "").Trim().ToLowerInvariant();
        input.Markdown ??= "";
        input.Summary ??= "";
        input.CoverUrl ??= "";
        input.Tags ??= "";
        var translations = ContentLanguages.Read<ContentTranslation>(input.Translations);
        if (translations.Count > 30 || translations.Any(t => !UiText.Supports(t.Key) || t.Value == null ||
            (t.Value.Title?.Length ?? 0) > 200 || (t.Value.Summary?.Length ?? 0) > 1000 || (t.Value.Markdown?.Length ?? 0) > 500000 ||
            (t.Value.CoverUrl?.Length ?? 0) > 1000 || !string.IsNullOrEmpty(t.Value.CoverUrl) && !MarkdownService.IsWebUrl(t.Value.CoverUrl)))
            throw new InvalidDataException(UiText.T("请检查内容译文的语言、长度和封面地址。"));
        input.Translations = ContentLanguages.Write(translations);
        var tags = input.Tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        input.Tags = string.Join(", ", tags);
        if (input.Title.Length > 200 || input.Summary.Length > 1000 || input.Markdown.Length > 500000 ||
            input.Slug.Length > 0 && !ShortLinks.IsValid(input.Slug) || tags.Length > 20 || tags.Any(t => t.Length > 50) ||
            input.Tags.Length > 1000 || input.CoverUrl.Length > 1000 || input.CoverUrl.Length > 0 && !MarkdownService.IsWebUrl(input.CoverUrl))
            throw new InvalidDataException(UiText.T("请检查长度、封面地址和短链接（小写字母、数字及连字符，最多 160 字符，不可使用系统保留路径）。标签最多 20 个，每个 50 字符。"));
        if (action != "autosave" && string.IsNullOrWhiteSpace(input.Title)) throw new InvalidDataException(UiText.T("请填写标题。"));
        db.Site.Transaction(() =>
        {
            var post = RequireEditable(id, user, true);
            if (post.Kind == PostKind.Page) { input.AllowComments = false; input.CategoryId = null; input.Tags = ""; tags = []; }
            if (input.Slug.Length == 0 && action != "autosave")
            {
                var stem = ShortLinks.FromTitle(input.Title, (post.Kind == PostKind.Page ? "new-page-" : "new-article-") + id);
                input.Slug = stem;
                for (var suffix = 2; db.Site.Select<Post>().Where(p => p.Slug == input.Slug && p.Id != id).Any(); suffix++)
                    input.Slug = stem + "-" + suffix;
            }
            if (input.CategoryId.HasValue && db.Site.Select<Category>().Where(c => c.Id == input.CategoryId).ForUpdate().First() == null)
                throw new InvalidDataException(UiText.T("分类不存在。"));
            var current = db.Site.Select<PostDraft>().Where(d => d.PostId == id).First();
            if (current.Version != input.Version) throw new System.Data.DBConcurrencyException(UiText.T("草稿已在另一个窗口修改；请先复制当前文字，再刷新页面，避免覆盖他人的修改。"));
            input.PostId = id;
            bool changed = current.Title != input.Title || current.Slug != input.Slug || current.Markdown != input.Markdown ||
                current.Summary != input.Summary || current.CoverUrl != input.CoverUrl || current.CategoryId != input.CategoryId ||
                current.Tags != input.Tags || current.AllowComments != input.AllowComments || current.Translations != input.Translations;
            if (changed)
            {
                input.Version++;
                input.UpdatedUtc = DateTime.UtcNow;
                db.Site.Update<PostDraft>().SetSource(input).ExecuteAffrows();
            }
            if (action == "autosave") return;
            var revision = new PostRevision {
                PostId = id, EditorId = AccountService.UserId(user), Title = input.Title, Slug = input.Slug,
                Summary = input.Summary, Markdown = input.Markdown, CoverUrl = input.CoverUrl, CategoryId = input.CategoryId,
                Tags = input.Tags, AllowComments = input.AllowComments, Translations = input.Translations,
                State = action == "submit" ? RevisionState.Pending : RevisionState.Saved
            };
            revision.Id = db.Site.Insert(revision).ExecuteIdentity();
            foreach (var name in tags)
            {
                var tag = db.Site.Select<Tag>().Where(t => t.Name == name).First();
                long tagId = tag?.Id ?? db.Site.Insert(new Tag { Name = name }).ExecuteIdentity();
                db.Site.Insert(new RevisionTag { RevisionId = revision.Id, TagId = tagId }).ExecuteAffrows();
            }
            if (action == "publish") Publish(post, revision);
        });
        return input.Version;
    }

    // Called only inside the post transaction, after checking publish permission.
    private void Publish(Post post, PostRevision revision)
    {
        if (!ShortLinks.IsValid(revision.Slug)) throw new InvalidDataException(UiText.T("短链接无效或使用了系统保留路径。"));
        if (db.Site.Select<Post>().Where(p => p.Slug == revision.Slug && p.Id != post.Id).Any())
            throw new InvalidDataException(UiText.T("短链接已被使用，请选择其他地址。"));
        post.PublishedRevisionId = revision.Id;
        post.Slug = revision.Slug;
        post.IsPublic = true;
        post.PublishedUtc ??= DateTime.UtcNow;
        db.Site.Update<Post>().SetSource(post).ExecuteAffrows();
        db.Site.Update<PostRevision>().Set(r => r.State, RevisionState.Published).Where(r => r.Id == revision.Id).ExecuteAffrows();
    }

    public void Review(long id, long revisionId, bool approve, ClaimsPrincipal user)
    {
        if (!AccountService.CanEditAll(user)) throw new UnauthorizedAccessException(UiText.T("需要审核权限。"));
        db.Site.Transaction(() =>
        {
            var post = RequireEditable(id, user, true);
            var revision = db.Site.Select<PostRevision>().Where(r => r.Id == revisionId && r.PostId == id && r.State == RevisionState.Pending).First();
            if (revision == null) throw new InvalidDataException(UiText.T("该修订已处理或不存在，请刷新页面。"));
            if (approve) Publish(post, revision);
            else db.Site.Update<PostRevision>().Set(r => r.State, RevisionState.Rejected).Where(r => r.Id == revisionId).ExecuteAffrows();
        });
    }

    public void Restore(long id, long revisionId, long version, ClaimsPrincipal user)
    {
        db.Site.Transaction(() =>
        {
            RequireEditable(id, user, true);
            var draft = db.Site.Select<PostDraft>().Where(d => d.PostId == id).First();
            if (draft.Version != version) throw new System.Data.DBConcurrencyException(UiText.T("草稿已修改，请刷新后再恢复。"));
            var revision = db.Site.Select<PostRevision>().Where(r => r.Id == revisionId && r.PostId == id).First();
            if (revision == null) throw new InvalidDataException(UiText.T("修订不存在。"));
            db.Site.Update<PostDraft>().SetSource(new PostDraft {
                PostId = id, Version = draft.Version + 1, Title = revision.Title, Slug = revision.Slug, Summary = revision.Summary,
                Markdown = revision.Markdown, CoverUrl = revision.CoverUrl, CategoryId = revision.CategoryId,
                Tags = revision.Tags, AllowComments = revision.AllowComments, Translations = revision.Translations
            }).ExecuteAffrows();
        });
    }

    public void Delete(long id, ClaimsPrincipal user)
    {
        db.Site.Transaction(() =>
        {
            var post = RequireEditable(id, user, true);
            if (post.PublishedRevisionId != null && !AccountService.CanEditAll(user))
                throw new UnauthorizedAccessException(UiText.T("已发布内容需由编辑或管理员删除。"));
            var revisions = db.Site.Select<PostRevision>().Where(r => r.PostId == id).ToList(r => r.Id);
            var comments = db.Site.Select<Comment>().Where(c => c.PostId == id).ToList(c => c.Id);
            db.Site.Delete<RevisionTag>().Where(t => revisions.Contains(t.RevisionId)).ExecuteAffrows();
            db.Site.Delete<CommentLike>().Where(l => comments.Contains(l.CommentId)).ExecuteAffrows();
            db.Site.Delete<Comment>().Where(c => c.PostId == id).ExecuteAffrows();
            db.Site.Delete<PostLike>().Where(l => l.PostId == id).ExecuteAffrows();
            db.Site.Delete<PostRevision>().Where(r => r.PostId == id).ExecuteAffrows();
            db.Site.Delete<PostDraft>().Where(d => d.PostId == id).ExecuteAffrows();
            db.Site.Delete<Post>().Where(p => p.Id == id).ExecuteAffrows();
        });
    }
}
