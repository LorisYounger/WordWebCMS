using System.Net;
using AngleSharp.Html.Parser;
using WordWebCMS.Data;
using WordWebCMS.Models;

internal static class CacheChecks
{
    public static async Task RunAsync(CmsDb db, HttpClient admin, Uri baseUrl, string storage, Action<bool, string> check)
    {
        using var visitor = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = baseUrl };
        var parser = new HtmlParser();
        async Task<HttpResponseMessage> Revalidate(string path, string etag, HttpClient? client = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.TryAddWithoutValidation("If-None-Match", etag);
            return await (client ?? visitor).SendAsync(request);
        }
        var originalTheme = db.Site.Select<SiteSettings>().First().Theme;
        try
        {
            foreach (var theme in new[] { "default", "milky" })
            {
                db.Site.Update<SiteSettings>().Set(s => s.Theme, theme).Where(s => s.Id == 1).ExecuteAffrows();
                foreach (var path in new[] { "/", "/about", "/welcome" })
                {
                    using var response = await visitor.GetAsync(path);
                    var html = await response.Content.ReadAsStringAsync();
                    check(response.IsSuccessStatusCode && response.Headers.CacheControl is { Public: true, NoCache: true, NoStore: false } &&
                        response.Headers.ETag != null && !response.Headers.Contains("Set-Cookie") && !html.Contains("__RequestVerificationToken"), theme + path + " visitor HTML is cacheable without CSRF cookies");
                    check(response.Headers.Vary.Contains("Cookie") && response.Headers.Vary.Contains("Accept-Language"), "HTML cache distinguishes cookies and language");
                    using var unchanged = await Revalidate(path, response.Headers.ETag!.ToString());
                    check(unchanged.StatusCode == HttpStatusCode.NotModified && (await unchanged.Content.ReadAsByteArrayAsync()).Length == 0 &&
                        Equals(unchanged.Headers.ETag, response.Headers.ETag), theme + path + " unchanged HTML returns an empty 304");
                    using var signedIn = await Revalidate(path, response.Headers.ETag.ToString(), admin);
                    check(signedIn.StatusCode == HttpStatusCode.OK && signedIn.Headers.CacheControl?.NoStore == true &&
                        (await signedIn.Content.ReadAsStringAsync()).Contains("__RequestVerificationToken"), "Visitor ETag cannot reuse HTML after login");
                }
                using var home = await visitor.GetAsync("/?culture=en");
                var tag = home.Headers.ETag!.ToString();
                using var changedLanguage = await Revalidate("/?culture=zh-Hans", tag);
                check(changedLanguage.StatusCode == HttpStatusCode.OK && changedLanguage.Headers.ETag!.ToString() != tag, "Language change invalidates HTML ETag");
                var doc = parser.ParseDocument(await home.Content.ReadAsStringAsync());
                var asset = doc.QuerySelector("link[href^='/theme-assets/']")!.GetAttribute("href")!;
                check(asset.Contains("?v="), "Theme asset URLs include content versions");
                using var css = await visitor.GetAsync(asset);
                check(css.Headers.CacheControl?.MaxAge == TimeSpan.FromDays(365) && css.Headers.CacheControl.Extensions.Any(e => e.Name == "immutable"), "Versioned theme assets are cached long term");
                using var css304 = await Revalidate(asset, css.Headers.ETag!.ToString());
                check(css304.StatusCode == HttpStatusCode.NotModified, "Theme assets support conditional requests");
                var cssPath = Path.Combine(storage, "theme", theme, "assets/site.css");
                var originalCss = await File.ReadAllTextAsync(cssPath);
                try
                {
                    await File.AppendAllTextAsync(cssPath, "\n/* cache regression */");
                    using var updated = await Revalidate("/?culture=en", tag);
                    var updatedDoc = parser.ParseDocument(await updated.Content.ReadAsStringAsync());
                    check(updated.StatusCode == HttpStatusCode.OK && updatedDoc.QuerySelector("link[href^='/theme-assets/']")!.GetAttribute("href") != asset,
                        "Changing a theme asset changes its URL and invalidates the page ETag");
                }
                finally { await File.WriteAllTextAsync(cssPath, originalCss); }
                var siteCss = doc.QuerySelector("link[href^='/css/ui.css']")!.GetAttribute("href")!;
                using var staticCss = await visitor.GetAsync(siteCss);
                check(siteCss.Contains("?v=") && staticCss.Headers.CacheControl?.MaxAge == TimeSpan.FromDays(365), "Versioned wwwroot assets are cached long term");
                using var static304 = await Revalidate(siteCss, staticCss.Headers.ETag!.ToString());
                check(static304.StatusCode == HttpStatusCode.NotModified, "wwwroot assets support conditional requests");
                using var legacy = await visitor.GetAsync(asset.Split('?')[0]);
                check(legacy.Headers.CacheControl is { Public: true, NoCache: true }, "Unversioned theme URLs remain revalidatable");
            }
            var custom = Path.Combine(storage, "theme", "cache-check");
            Directory.CreateDirectory(custom);
            await File.WriteAllTextAsync(Path.Combine(custom, "page.liquid"), "{{ post.html | raw }}");
            await File.WriteAllTextAsync(Path.Combine(custom, "layout.liquid"), "{{ body | raw }}{% if signed_in %}{{ csrf }}{% endif %}");
            db.Site.Update<SiteSettings>().Set(s => s.Theme, "cache-check").Where(s => s.Id == 1).ExecuteAffrows();
            using var skipped = await visitor.GetAsync("/about");
            check(skipped.Headers.CacheControl?.NoStore == false && !skipped.Headers.Contains("Set-Cookie"), "Unexecuted csrf branch does not generate tokens");
            await File.WriteAllTextAsync(Path.Combine(custom, "layout.liquid"), "{{ body | raw }}<form method='post'><input name='__RequestVerificationToken' value='{{ csrf }}'></form>");
            using var anonymousForm = await visitor.GetAsync("/about");
            var form = parser.ParseDocument(await anonymousForm.Content.ReadAsStringAsync());
            check(anonymousForm.Headers.CacheControl?.NoStore == true && anonymousForm.Headers.Contains("Set-Cookie") &&
                !string.IsNullOrEmpty(form.QuerySelector("input")?.GetAttribute("value")), "Custom anonymous POST forms receive a token and cannot be cached");
            await File.WriteAllTextAsync(Path.Combine(custom, "layout.liquid"), "{{ body | raw }}");
            using var noForm = await admin.GetAsync("/about");
            check(noForm.Headers.CacheControl?.NoStore == true && !noForm.Headers.Contains("Set-Cookie"), "Authenticated custom pages without forms do not generate tokens");
        }
        finally { db.Site.Update<SiteSettings>().Set(s => s.Theme, originalTheme).Where(s => s.Id == 1).ExecuteAffrows(); }

        using var login = await visitor.GetAsync("/Account/Login");
        check(login.Headers.CacheControl?.NoStore == true && (await login.Content.ReadAsStringAsync()).Contains("__RequestVerificationToken"), "Login retains Razor antiforgery and no-store");
        using var forged = await admin.PostAsync("/welcome?handler=Like", new FormUrlEncodedContent(new Dictionary<string, string> { ["liked"] = "true" }));
        check(forged.StatusCode == HttpStatusCode.BadRequest, "POST without a token is still rejected");
        using var error = await visitor.GetAsync("/cache-test-missing");
        check(error.StatusCode == HttpStatusCode.NotFound && error.Headers.CacheControl?.NoStore == true, "Error responses are not cached");
        using var tempDataRequest = new HttpRequestMessage(HttpMethod.Get, "/welcome");
        tempDataRequest.Headers.TryAddWithoutValidation("Cookie", ".AspNetCore.Mvc.CookieTempDataProvider=invalid");
        using var tempData = await visitor.SendAsync(tempDataRequest);
        check(tempData.Headers.CacheControl?.NoStore == true, "TempData cookie responses cannot enter public caches");

        var filename = Guid.NewGuid().ToString("N") + ".png";
        var media = new Media { FileName = filename, OriginalName = "cache-test.png", ContentType = "image/png", Size = 8 };
        media.Id = db.Site.Insert(media).ExecuteIdentity();
        var mediaPath = Path.Combine(storage, "media", filename);
        await File.WriteAllBytesAsync(mediaPath, [137, 80, 78, 71, 13, 10, 26, 10]);
        try
        {
            using var response = await visitor.GetAsync("/media/" + filename);
            check(response.IsSuccessStatusCode && response.Headers.CacheControl?.MaxAge == TimeSpan.FromDays(365) && response.Headers.ETag != null, "Unique media filenames are cached long term");
            using var unchanged = await Revalidate("/media/" + filename, response.Headers.ETag!.ToString());
            check(unchanged.StatusCode == HttpStatusCode.NotModified, "Media supports conditional requests");
        }
        finally { File.Delete(mediaPath); db.Site.Delete<Media>().Where(m => m.Id == media.Id).ExecuteAffrows(); }
    }
}
