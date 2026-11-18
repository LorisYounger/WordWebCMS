using System.Net;
using System.Security.Claims;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WordWebCMS.Data;
using WordWebCMS.Models;
using WordWebCMS.Services;

internal static class MigrationFeatureChecks
{
    public static async Task RunAsync(CmsDb db, HttpClient admin, CmsUser owner, string storage, Action<bool, string> check)
    {
        var parser = new HtmlParser();
        var markdown = new MarkdownService();
        var rendered = parser.ParseDocument(markdown.Render("# Hello world\n\n# Hello world\n\n<h2 id='install' style='color:red;position:fixed;background-image:url(javascript:alert(1))'>Install</h2><span style='font-size:12pt'>Keep text</span><div id='evil' onclick='alert(1)'>Body</div><script>alert(1)</script><img src='x.png' onerror='alert(1)' width='9000'>"));
        check(rendered.QuerySelectorAll("h1[id]").Length == 2 && rendered.QuerySelectorAll("h1").Select(e => e.Id).Distinct().Count() == 2 && rendered.QuerySelector("h2#install") != null, "Generated and imported heading anchors are stable and unique");
        check(rendered.QuerySelector("span")?.TextContent == "Keep text" && (rendered.QuerySelector("h2")?.GetAttribute("style") ?? "").Contains("color"), "Limited presentation styles and their text survive");
        check(rendered.QuerySelector("script,[onclick],[onerror],div[id],img[width]") == null && !rendered.Body!.InnerHtml.Contains("position:") && !rendered.Body.InnerHtml.Contains("background-image"), "Unsafe markup, CSS and invalid dimensions are removed");
        check(parser.ParseDocument(markdown.Render("<h2 id='x'>text</h2>", true)).QuerySelector("h2") == null, "Comment raw HTML remains disabled");
        using var services = new ServiceCollection().AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Setup:Token"] = "test-secret" }).Build()).BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.1");
        var hash = LikeIdentity.Anonymous(context);
        context.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:192.0.2.1"); context.Request.Headers["X-Forwarded-For"] = "203.0.113.1";
        check(hash.Length == 64 && hash == LikeIdentity.Anonymous(context) && !hash.Contains("192.0.2.1"), "Anonymous identity normalizes mapped IPs, hashes them and ignores raw proxy headers");
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.2");
        check(hash != LikeIdentity.Anonymous(context), "Distinct IPs have distinct anonymous identities");

        var content = new ContentService(db);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, owner.Id.ToString()), new Claim(ClaimTypes.Role, "Administrator")], "test"));
        var id = content.Create(PostKind.Article, principal);
        var draft = db.Site.Select<PostDraft>().Where(d => d.PostId == id).First(); draft.Title = "Private test marker"; draft.Slug = "migration-feature-test"; draft.Markdown = "# Anchor\n\nPrivate body";
        content.Save(id, draft, principal, "publish");
        db.Site.Update<Post>().Set(p => p.LegacyLikes, 12).Where(p => p.Id == id).ExecuteAffrows();
        var comment = new Comment { PostId = id, UserId = owner.Id, Approved = true, Markdown = "Approved comment" }; comment.Id = db.Site.Insert(comment).ExecuteIdentity();
        using var guest = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = admin.BaseAddress };
        using var second = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = admin.BaseAddress };
        async Task<Dictionary<string, string>> Form(HttpClient client, string path) => new() { ["__RequestVerificationToken"] = parser.ParseDocument(await client.GetStringAsync(path)).QuerySelector("input[name=__RequestVerificationToken]")!.GetAttribute("value")! };
        async Task<HttpResponseMessage> Send(HttpClient client, string path, Dictionary<string, string> data) => await client.PostAsync(path, new FormUrlEncodedContent(data));
        try
        {
            var form = await Form(guest, "/migration-feature-test"); form["liked"] = "true";
            check((await Send(guest, "/migration-feature-test?handler=Like", form)).StatusCode == HttpStatusCode.Redirect, "Anonymous article like accepts CSRF token");
            var again = await Form(second, "/migration-feature-test"); again["liked"] = "true";
            await Send(second, "/migration-feature-test?handler=Like", again);
            check(db.Site.Select<AnonymousPostLike>().Where(l => l.PostId == id).Count() == 1, "Same IP across sessions has only one article like");
            form["commentId"] = comment.Id.ToString(); await Send(guest, "/migration-feature-test?handler=Like", form); await Send(guest, "/migration-feature-test?handler=Like", form);
            check(db.Site.Select<AnonymousCommentLike>().Where(l => l.CommentId == comment.Id).Count() == 1, "Anonymous comment likes are idempotent");
            form["liked"] = "false"; await Send(guest, "/migration-feature-test?handler=Like", form); form.Remove("commentId"); await Send(guest, "/migration-feature-test?handler=Like", form);
            check(!db.Site.Select<AnonymousPostLike>().Where(l => l.PostId == id).Any() && !db.Site.Select<AnonymousCommentLike>().Where(l => l.CommentId == comment.Id).Any() && db.Site.Select<Post>().Where(p => p.Id == id).First().LegacyLikes == 12, "Anonymous unlike removes only visitor likes, preserving imported count");
            check((await Send(guest, "/migration-feature-test?handler=Like", new() { ["liked"] = "true" })).StatusCode == HttpStatusCode.BadRequest, "Anonymous like rejects missing CSRF token");
            draft = db.Site.Select<PostDraft>().Where(d => d.PostId == id).First(); content.Save(id, draft, principal, "private");
            check((await guest.GetAsync("/migration-feature-test")).StatusCode == HttpStatusCode.NotFound, "Private article is hidden from anonymous visitors");
            using var privatePage = await admin.GetAsync("/migration-feature-test");
            check(privatePage.IsSuccessStatusCode && privatePage.Headers.CacheControl?.NoStore == true && privatePage.Headers.Contains("X-Robots-Tag"), "Author/admin can read private article without shared caching or indexing");
            var post = db.Site.Select<Post>().Where(p => p.Id == id).First();
            check(!ContentService.CanRead(post, new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "99999"), new Claim(ClaimTypes.Role, "Reader")], "test"))), "Other signed-in readers cannot read private content");
            check(ContentService.CanRead(post, new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, owner.Id.ToString()), new Claim(ClaimTypes.Role, "Contributor")], "test"))), "Private author retains read access without editor role");
            foreach (var path in new[] { "/search/Private", "/feed.xml", "/sitemap.xml" }) check(!(await guest.GetStringAsync(path)).Contains("migration-feature-test"), "Private content excluded from " + path);
            check((await Send(guest, "/migration-feature-test?handler=Like", form)).StatusCode == HttpStatusCode.Conflict, "Anonymous POST cannot interact with private content");
            var visibility = await Form(admin, "/Admin"); visibility["id"] = id.ToString(); visibility["visible"] = "false";
            await Send(admin, "/Admin?handler=Visibility", visibility);
            check((await admin.GetAsync("/migration-feature-test")).StatusCode == HttpStatusCode.NotFound, "Withdrawn content remains unavailable even to authors on the front end");
            db.Site.Insert(new AnonymousCommentLike { CommentId = comment.Id, IpHash = hash }).ExecuteAffrows();
            var remove = await Form(admin, "/Admin/Comments?all=true"); remove["id"] = comment.Id.ToString(); remove["action"] = "delete";
            check((await Send(admin, "/Admin/Comments", remove)).StatusCode == HttpStatusCode.Redirect && !db.Site.Select<AnonymousCommentLike>().Where(l => l.CommentId == comment.Id).Any(), "Deleting a comment removes its anonymous likes");
        }
        finally { content.Delete(id, principal); }

        var filename = Guid.NewGuid().ToString("N") + ".png";
        var media = new Media { FileName = filename, LinkName = "test/自定义.png", OriginalName = "test.png", ContentType = "image/png", UserId = owner.Id, Size = 12 };
        media.Id = db.Site.Insert(media).ExecuteIdentity(); var filePath = Path.Combine(storage, "media", filename); await File.WriteAllBytesAsync(filePath, [137,80,78,71,13,10,26,10,0,0,0,0]);
        try
        {
            using var response = await guest.GetAsync(MediaFiles.Url(media));
            check(response.IsSuccessStatusCode && response.Headers.CacheControl?.NoCache == true && (await response.Content.ReadAsByteArrayAsync()).Length == 12, "Custom Unicode media paths resolve stored files with revalidation");
            var form = await Form(admin, "/Admin/Media"); form["id"] = media.Id.ToString(); form["linkName"] = "images/renamed.png";
            check((await Send(admin, "/Admin/Media?handler=Link", form)).StatusCode == HttpStatusCode.Redirect && File.Exists(filePath), "Editing media link does not rename physical file");
            check((await guest.GetAsync("/media/images/renamed.png")).IsSuccessStatusCode && (await guest.GetAsync(MediaFiles.Url(media))).StatusCode == HttpStatusCode.NotFound, "Renamed media URL serves new alias and retires old alias");
            foreach (var bad in new[] { "../escape.png", "/absolute.png", "a%2fb.png", "x.php", "x\\y.png" }) { form["linkName"] = bad; check((await Send(admin, "/Admin/Media?handler=Link", form)).StatusCode == HttpStatusCode.BadRequest, "Reject unsafe media link: " + bad); }
            var duplicate = new Media { FileName = "reserved.png", LinkName = "reserved.png" }; duplicate.Id = db.Site.Insert(duplicate).ExecuteIdentity();
            try { form["linkName"] = "reserved.png"; check((await Send(admin, "/Admin/Media?handler=Link", form)).StatusCode == HttpStatusCode.BadRequest, "Duplicate media alias is rejected without overwriting"); }
            finally { db.Site.Delete<Media>().Where(m => m.Id == duplicate.Id).ExecuteAffrows(); }
        }
        finally { File.Delete(filePath); db.Site.Delete<Media>().Where(m => m.Id == media.Id).ExecuteAffrows(); }
    }
}
