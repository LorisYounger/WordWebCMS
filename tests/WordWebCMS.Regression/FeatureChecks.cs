using System.Net;
using System.Security.Claims;
using AngleSharp.Html.Parser;
using WordWebCMS.Data;
using WordWebCMS.Models;
using WordWebCMS.Services;

internal static class FeatureChecks
{
    public static async Task RunAsync(CmsDb db, HttpClient admin, HttpClient visitor, CmsUser owner, Action<bool, string> check)
    {
        var parser = new HtmlParser();
        var content = new ContentService(db);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, owner.Id.ToString()), new Claim(ClaimTypes.Role, "Administrator")], "test"));
        var category = new Category { Name = "公告" };
        category.Id = db.Site.Insert(category).ExecuteIdentity();
        var special = new Category { Name = ".NET / C# 100% %2F" };
        special.Id = db.Site.Insert(special).ExecuteIdentity();
        var posts = new List<long>();
        var stamp = new DateTime(2026, 10, 28, 23, 30, 0, DateTimeKind.Utc);
        for (var i = 0; i < 12; i++)
        {
            var id = content.Create(PostKind.Article, principal);
            posts.Add(id);
            var draft = db.Site.Select<PostDraft>().Where(d => d.PostId == id).First();
            draft.Title = "Feature article " + i; draft.Slug = "feature-article-" + i;
            draft.Markdown = "Feature body"; draft.CategoryId = category.Id; draft.Tags = "测试标签";
            draft.Translations = "{\"en\":{\"Title\":\"Translated feature\",\"Markdown\":\"Translated body\"}}";
            content.Save(id, draft, principal, "publish");
            db.Site.Update<Post>().Set(p => p.PublishedUtc, stamp).Where(p => p.Id == id).ExecuteAffrows();
        }
        var other = new CmsUser { UserName = "feature-other-author", Email = "feature-other@example.test", EmailConfirmed = true };
        other.Id = db.Accounts.Insert(other).ExecuteIdentity();
        db.Site.Insert(new SiteUserRole { UserId = other.Id, Role = SiteRole.Contributor }).ExecuteAffrows();
        db.Site.Update<Post>().Set(p => p.AuthorId, other.Id).Where(p => p.Id == posts[11]).ExecuteAffrows();
        var tag = db.Site.Select<Tag>().Where(t => t.Name == "测试标签").First();
        async Task<AngleSharp.Dom.IDocument> Document(string path, HttpClient? client = null) => parser.ParseDocument(await (client ?? visitor).GetStringAsync(path));
        async Task<string> Token(string path) => (await Document(path, admin)).QuerySelector("input[name=__RequestVerificationToken]")!.GetAttribute("value")!;
        var categoryPath = PublicUrls.Category(category.Name);
        foreach (var (oldUrl, newUrl) in new[] {
            ($"/?category={category.Id}", categoryPath), ($"/?tag={tag.Id}", PublicUrls.Tag(tag.Name)),
            ("/?year=2026&month=10", "/archive/2026/10"), ("/?q=Feature", "/search/Feature"),
            ($"/?category={category.Id}&p=2&culture=en", categoryPath + "/page/2?culture=en") })
        {
            var response = await visitor.GetAsync(oldUrl);
            check(response.StatusCode == HttpStatusCode.MovedPermanently && Uri.UnescapeDataString(response.Headers.Location!.ToString()) == Uri.UnescapeDataString(newUrl), "Legacy list redirect: " + oldUrl);
        }
        var list = await Document(categoryPath);
        check(list.QuerySelectorAll(".post-card").Length == 10 && list.QuerySelector(".pagination a")?.GetAttribute("href") == categoryPath + "/page/2", "Category pagination keeps readable route");
        check((await Document(categoryPath + "/page/2")).QuerySelectorAll(".post-card").Length == 2, "Second category page contains remaining articles");
        check((await Document(PublicUrls.List(category.Name, tag.Name, 2026, 10, "Feature", owner.UserName, 1))).QuerySelectorAll(".post-card").Length == 10, "Combined path filters retain their intersection");
        check((await visitor.GetAsync(PublicUrls.Category(special.Name))).StatusCode == HttpStatusCode.OK, "Category names containing slash, hash and percent round trip");
        check((await visitor.GetAsync("/category/missing")).StatusCode == HttpStatusCode.NotFound && (await visitor.GetAsync("/archive/2026/13")).StatusCode == HttpStatusCode.BadRequest, "Unknown and invalid filters do not show all articles");
        check((await visitor.GetAsync("/search/a%2Fb%20%23100%25")).StatusCode == HttpStatusCode.OK, "Search punctuation round trips through a path");
        var postDoc = await Document("/feature-article-0");
        check(postDoc.QuerySelector(".article-header .author-link")?.GetAttribute("href") == PublicUrls.Author(owner.UserName) && postDoc.QuerySelectorAll(".article-header .avatar").Length == 1, "Article author avatar and name share one profile link");
        check(postDoc.QuerySelector("time[data-local-time]")?.GetAttribute("datetime") == "2026-10-28T23:30:00.000Z", "Public timestamps are UTC instants with Z");
        check(db.Site.Select<Post>().Where(p => p.Id == posts[0]).First().PublishedUtc == stamp, "MySQL preserves UTC without server-local adjustment");
        check(PublicUrls.Utc(DateTime.SpecifyKind(stamp, DateTimeKind.Unspecified)) == "2026-10-28T23:30:00.000Z", "Unspecified MySQL timestamps serialize as UTC");
        check((await visitor.GetAsync("/Account/Profile")).StatusCode == HttpStatusCode.Redirect, "Profile editing requires authentication");
        check((await admin.PostAsync("/Account/Profile", new FormUrlEncodedContent(new Dictionary<string,string> { ["Bio"] = "forged" }))).StatusCode == HttpStatusCode.BadRequest, "Profile editing requires CSRF token");
        var bio = "Writes about software.\n<script>alert(1)</script>";
        var responseProfile = await admin.PostAsync("/Account/Profile", new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"] = await Token("/Account/Profile"), ["Bio"] = bio, ["UserId"] = "99999" }));
        check(responseProfile.Headers.Location?.ToString() == PublicUrls.Author(owner.UserName) && db.Accounts.Select<CmsUser>().Where(u => u.Id == owner.Id).First().Bio == bio, "Profile edit updates the authenticated user's introduction");
        var profile = await Document(PublicUrls.Author(owner.UserName));
        check(profile.QuerySelector(".profile-bio")?.TextContent == bio && profile.QuerySelector(".profile-bio script") == null, "Profile bio is escaped and never exposes executable HTML");
        check(profile.QuerySelector(".profile-card img") != null && profile.QuerySelectorAll(".post-card").Length > 0 && profile.QuerySelectorAll(".post-card a").All(a => a.GetAttribute("href") != "/about"), "Profile contains avatar, bio and author's articles, excluding pages");
        check(profile.QuerySelectorAll(".post-card a").All(a => a.GetAttribute("href") != "/feature-article-11"), "Profile excludes articles written by another author");
        check(!profile.DocumentElement.OuterHtml.Contains(owner.Email) && !profile.DocumentElement.OuterHtml.Contains(owner.PasswordHash), "Public profile does not expose account secrets");
        check((await Document(PublicUrls.Author(owner.UserName), admin)).QuerySelector(".profile-card a[href='/Account/Profile']") != null, "Profile owner can find the edit action");
        check((await visitor.GetAsync("/author/unknown-user")).StatusCode == HttpStatusCode.NotFound, "Unknown author returns 404");
        if (Environment.GetEnvironmentVariable("WWCMS_BROWSER_CHECKS") == "1")
        {
            try
            {
                foreach (var theme in new[] { "default", "milky" })
                {
                    db.Site.Update<SiteSettings>().Set(s => s.Theme, theme).Where(s => s.Id == 1).ExecuteAffrows();
                    var start = new System.Diagnostics.ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true };
                    start.ArgumentList.Add("tests/WordWebCMS.Regression/browser-features.cjs");
                    start.ArgumentList.Add(visitor.BaseAddress!.ToString().TrimEnd('/'));
                    start.ArgumentList.Add(theme); start.ArgumentList.Add(owner.UserName);
                    using var browser = System.Diagnostics.Process.Start(start)!;
                    await browser.WaitForExitAsync();
                    check(browser.ExitCode == 0, "Browser acceptance: " + theme);
                }
            }
            finally { db.Site.Update<SiteSettings>().Set(s => s.Theme, "default").Where(s => s.Id == 1).ExecuteAffrows(); }
        }
        var idToConvert = posts[0];
        var oldPost = db.Site.Select<Post>().Where(p => p.Id == idToConvert).First();
        var oldPublished = oldPost.PublishedRevisionId!.Value;
        db.Site.Update<Post>().Set(p => p.PinOrder, 9).Where(p => p.Id == idToConvert).ExecuteAffrows();
        var commentId = db.Site.Insert(new Comment { PostId = idToConvert, UserId = owner.Id, Markdown = "Conversion comment", Approved = true }).ExecuteIdentity();
        db.Site.Insert(new CommentLike { CommentId = commentId, UserId = owner.Id }).ExecuteAffrows();
        db.Site.Insert(new PostLike { PostId = idToConvert, UserId = owner.Id }).ExecuteAffrows();
        var draftToConvert = db.Site.Select<PostDraft>().Where(d => d.PostId == idToConvert).First();
        content.Save(idToConvert, draftToConvert, principal, "submit");
        var contributor = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, owner.Id.ToString()), new Claim(ClaimTypes.Role, "Contributor")], "test"));
        try { content.Convert(idToConvert, PostKind.Page, draftToConvert.Version, contributor); check(false, "Published conversion authorization"); }
        catch (UnauthorizedAccessException) { check(true, "Contributor cannot convert published content"); }
        var editPath = "/Admin/Edit/" + idToConvert;
        var form = new Dictionary<string,string> { ["__RequestVerificationToken"] = await Token(editPath), ["Draft.Version"] = draftToConvert.Version.ToString(), ["Draft.Title"] = "Unsaved conversion title", ["Draft.Slug"] = oldPost.Slug, ["Draft.Markdown"] = "Unsaved conversion body", ["action"] = "convert-page" };
        var convertResponse = await admin.PostAsync(editPath, new FormUrlEncodedContent(form));
        if (convertResponse.StatusCode != HttpStatusCode.Redirect) Console.WriteLine("Conversion response: " + convertResponse.StatusCode + " " + await convertResponse.Content.ReadAsStringAsync());
        check(convertResponse.StatusCode == HttpStatusCode.Redirect, "Editor saves current text and converts to page");
        var converted = db.Site.Select<Post>().Where(p => p.Id == idToConvert).First();
        var draftAfter = db.Site.Select<PostDraft>().Where(d => d.PostId == idToConvert).First();
        var publishedAfter = db.Site.Select<PostRevision>().Where(r => r.Id == converted.PublishedRevisionId).First();
        check(converted.Kind == PostKind.Page && converted.Slug == oldPost.Slug && converted.AuthorId == oldPost.AuthorId && converted.PublishedUtc == stamp && converted.PinOrder == 0, "Conversion preserves identity and publication date while clearing pin");
        check(draftAfter.Title == "Unsaved conversion title" && draftAfter.Markdown == "Unsaved conversion body" && draftAfter.Translations.Contains("Translated feature"), "Conversion retains unsaved draft text and translations");
        check(publishedAfter.Title == "Feature article 0" && publishedAfter.CategoryId == null && publishedAfter.Tags == "" && !publishedAfter.AllowComments, "Conversion does not publish draft edits and removes article-only publication fields");
        check(!db.Site.Select<Comment>().Where(c => c.PostId == idToConvert).Any() && !db.Site.Select<CommentLike>().Where(l => l.CommentId == commentId).Any() && !db.Site.Select<PostLike>().Where(l => l.PostId == idToConvert).Any(), "Conversion discards comments and all likes");
        check(!db.Site.Select<PostRevision>().Where(r => r.PostId == idToConvert && r.State == RevisionState.Pending).Any(), "Conversion invalidates pending revisions from the previous type");
        check((await Document("/" + oldPost.Slug)).QuerySelector("#comments, .sidebar") == null, "Converted page immediately uses standalone page presentation");
        check((await admin.PostAsync(editPath, new FormUrlEncodedContent(form))).StatusCode == HttpStatusCode.Conflict, "Stale editor cannot overwrite a converted draft");
        content.Restore(idToConvert, oldPublished, draftAfter.Version, principal);
        draftAfter = db.Site.Select<PostDraft>().Where(d => d.PostId == idToConvert).First();
        check(draftAfter.CategoryId == null && draftAfter.Tags == "" && !draftAfter.AllowComments, "Restoring article history to a page cannot resurrect unsupported fields");
        content.Convert(idToConvert, PostKind.Article, draftAfter.Version, principal);
        check(db.Site.Select<Post>().Where(p => p.Id == idToConvert).First().Kind == PostKind.Article && (await Document("/" + oldPost.Slug)).QuerySelector("#comments") != null, "Page converts back to article at the original URL");
        foreach (var id in posts) content.Delete(id, principal);
        db.Site.Delete<SiteUserRole>().Where(r => r.UserId == other.Id).ExecuteAffrows();
        db.Accounts.Delete<CmsUser>().Where(u => u.Id == other.Id).ExecuteAffrows();
        db.Site.Delete<Category>().Where(c => c.Id == category.Id || c.Id == special.Id).ExecuteAffrows();
        db.Site.Delete<Tag>().Where(t => t.Id == tag.Id).ExecuteAffrows();
        db.Accounts.Update<CmsUser>().Set(u => u.Bio, "").Where(u => u.Id == owner.Id).ExecuteAffrows();
    }
}

