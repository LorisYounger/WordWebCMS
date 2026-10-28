using System.Security.Claims;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WordWebCMS.Pages;
using WordWebCMS.Services;

internal static class CacheLocalChecks
{
    public static async Task RunAsync(string root)
    {
        var storage = Path.Combine(root, "artifacts", "cache-local-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(storage);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = Path.Combine(root, "WordWebCMS"), WebRootPath = "wwwroot" });
        builder.Logging.ClearProviders();
        builder.Services.AddRazorPages();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddSingleton(new StoragePaths(storage));
        builder.Services.AddSingleton<ThemeService>();
        builder.Services.AddSingleton<MarkdownService>();
        await using var app = builder.Build();
        var themeService = app.Services.GetRequiredService<ThemeService>();
        themeService.InitializeDefault();
        UiText.Load(Path.Combine(root, "WordWebCMS/Languages"), Path.Combine(storage, "languages"));
        var parser = new HtmlParser();
        var count = 0;
        void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); count++; Console.WriteLine("PASS: " + name); }
        DefaultHttpContext Context(bool signedIn = false, string method = "GET")
        {
            var context = new DefaultHttpContext { RequestServices = app.Services };
            context.Request.Method = method;
            context.Request.Scheme = "https";
            context.Request.Host = new HostString("cache.example.test");
            context.Response.Body = new MemoryStream();
            if (signedIn) context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Name, "reader")], "test"));
            return context;
        }
        foreach (var theme in new[] { "default", "milky" })
        foreach (var template in new[] { "list.liquid", "post.liquid", "page.liquid" })
        {
            var data = new Dictionary<string, object?> { ["signed_in"] = false };
            var visitor = Context();
            var html = await themeService.RenderAsync(theme, template, data, visitor);
            var result = HttpCache.Content(visitor, html, "text/html; charset=utf-8");
            var label = theme + "/" + template;
            Check(!html.Contains("__RequestVerificationToken") && !visitor.Response.Headers.ContainsKey("Set-Cookie") &&
                visitor.Response.GetTypedHeaders().CacheControl is { Public: true, NoCache: true, NoStore: false }, label + " anonymous output is cacheable without tokens");
            var etag = visitor.Response.Headers.ETag.ToString();
            var next = Context();
            next.Request.Headers.IfNoneMatch = "\"unrelated\", " + etag;
            var notModified = HttpCache.Content(next, html, "text/html; charset=utf-8");
            await notModified.ExecuteResultAsync(new ActionContext(next, new RouteData(), new ActionDescriptor()));
            Check(next.Response.StatusCode == 304 && next.Response.Body.Length == 0 && next.Response.Headers.ETag == etag, label + " unchanged content returns empty 304");
            Check(next.Response.Headers.Vary.ToString().Contains("Cookie") && next.Response.Headers.Vary.ToString().Contains("Accept-Language"), "304 preserves cache variants");
            var changed = Context(); changed.Request.Headers.IfNoneMatch = etag;
            Check(HttpCache.Content(changed, html + "updated", "text/html; charset=utf-8") is ContentResult { StatusCode: 200 } && changed.Response.Headers.ETag != etag, "Content updates invalidate cached HTML");
            var signedIn = Context(true); signedIn.Request.Headers.IfNoneMatch = etag;
            data["signed_in"] = true;
            var signedHtml = await themeService.RenderAsync(theme, template, data, signedIn);
            var signedResult = HttpCache.Content(signedIn, signedHtml, "text/html; charset=utf-8");
            Check(signedResult is ContentResult { StatusCode: 200 } && signedHtml.Contains("__RequestVerificationToken") && signedIn.Response.GetTypedHeaders().CacheControl?.NoStore == true,
                label + " login ignores public ETag and protects forms");
            var token = parser.ParseDocument(signedHtml).QuerySelector("input[name=__RequestVerificationToken]")!.GetAttribute("value")!;
            var cookie = signedIn.Response.Headers.SetCookie.Single(value => value!.StartsWith(".AspNetCore.Antiforgery"))!.Split(';')[0];
            var post = Context(true, "POST");
            post.Request.Headers.Cookie = cookie;
            post.Request.ContentType = "application/x-www-form-urlencoded";
            post.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues> { ["__RequestVerificationToken"] = token });
            var antiforgery = app.Services.GetRequiredService<IAntiforgery>();
            Check(await antiforgery.IsRequestValidAsync(post), "Rendered token passes real framework POST validation");
            post.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>());
            // A fresh request avoids the antiforgery feature's per-request token cache.
            var forged = Context(true, "POST"); forged.Request.Headers.Cookie = cookie;
            forged.Request.ContentType = "application/x-www-form-urlencoded";
            forged.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>());
            Check(!await antiforgery.IsRequestValidAsync(forged), "POST without token fails real framework validation");
        }
        var wildcard = Context(); wildcard.Request.Headers.IfNoneMatch = "*";
        Check(HttpCache.Content(wildcard, "body", "text/html") is StatusCodeResult { StatusCode: 304 }, "Wildcard If-None-Match returns 304 for an existing public representation");
        foreach (var kind in new[] { "cookie", "tempdata", "authorization", "error", "post", "authenticated" })
        {
            var context = Context(kind == "authenticated", kind == "post" ? "POST" : "GET");
            if (kind == "cookie") context.Response.Cookies.Append("example", "value");
            if (kind == "tempdata") context.Request.Headers.Cookie = ".AspNetCore.Mvc.CookieTempDataProvider=notice";
            if (kind == "authorization") context.Request.Headers.Authorization = "Bearer test";
            HttpCache.Content(context, "body", "text/html", kind == "error" ? 404 : 200);
            Check(context.Response.GetTypedHeaders().CacheControl?.NoStore == true && !context.Response.Headers.ContainsKey("ETag"), kind + " response cannot enter public cache");
        }
        var custom = Path.Combine(storage, "theme/cache-check"); Directory.CreateDirectory(custom);
        await File.WriteAllTextAsync(Path.Combine(custom, "page.liquid"), "content");
        await File.WriteAllTextAsync(Path.Combine(custom, "layout.liquid"), "{{ body | raw }}{% if signed_in %}{{ csrf }}{% endif %}");
        var skipped = Context();
        await themeService.RenderAsync("cache-check", "page.liquid", new() { ["signed_in"] = false }, skipped);
        Check(!skipped.Response.Headers.ContainsKey("Set-Cookie"), "Skipped custom csrf branch generates no token");
        await File.WriteAllTextAsync(Path.Combine(custom, "layout.liquid"), "<form method='post'><input value='{{ csrf }}'></form>");
        var customForm = Context();
        var customHtml = await themeService.RenderAsync("cache-check", "page.liquid", new(), customForm);
        Check(customForm.Response.Headers.ContainsKey("Set-Cookie") && customForm.Response.GetTypedHeaders().CacheControl?.NoStore == true &&
            !string.IsNullOrEmpty(parser.ParseDocument(customHtml).QuerySelector("input")?.GetAttribute("value")), "Custom anonymous POST form gets a real token");
        foreach (var theme in new[] { "default", "milky" })
        {
            async Task<string> CssUrl() => parser.ParseDocument(await themeService.RenderAsync(theme, "list.liquid", new())).QuerySelector("link[href^='/theme-assets/']")!.GetAttribute("href")!;
            var url = await CssUrl();
            Check(url.Contains("?v="), "Theme CSS has content version");
            var context = Context(); context.Request.QueryString = new QueryString(url[url.IndexOf('?')..]);
            var model = new ThemeAssetModel(new StoragePaths(storage)) { PageContext = new PageContext { HttpContext = context } };
            var fileResult = (PhysicalFileResult)model.OnGet(theme, "assets/site.css");
            Check(context.Response.GetTypedHeaders().CacheControl?.MaxAge == TimeSpan.FromDays(365), "Versioned theme asset has long cache lifetime");
            var conditional = Context(); conditional.Request.Headers.IfNoneMatch = fileResult.EntityTag!.ToString();
            await fileResult.ExecuteResultAsync(new ActionContext(conditional, new RouteData(), new ActionDescriptor()));
            Check(conditional.Response.StatusCode == 304 && conditional.Response.Body.Length == 0, "Real physical-file executor returns empty 304");
            await File.AppendAllTextAsync(Path.Combine(storage, "theme", theme, "assets/site.css"), "\n/* updated */");
            Check(await CssUrl() != url, "Changed theme CSS gets a new content URL");
        }
        Console.WriteLine($"All {count} database-free cache checks passed.");
    }
}
