using System.IO.Compression;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Fluid;
using Fluid.Values;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using WordWebCMS.Data;
using WordWebCMS.Models;

namespace WordWebCMS.Services;

public class ThemeService(StoragePaths paths, IWebHostEnvironment env, IAntiforgery antiforgery)
{
    public static readonly string[] Templates = ["layout.liquid", "list.liquid", "post.liquid", "error.liquid"];
    private readonly FluidParser parser = new();

    public async Task<ContentResult> PageAsync(CmsDb db, HttpContext http, string template, Dictionary<string, object?> data, int status = 200)
    {
        var settings = http.Items["SiteSettings"] as SiteSettings ?? await db.Site.Select<SiteSettings>().FirstAsync();
        var navigation = await db.Site.Select<NavigationItem>().OrderBy(n => n.SortOrder).OrderBy(n => n.Id).ToListAsync();
        var siteText = ContentLanguages.Display(settings);
        data["site"] = new Dictionary<string, object?> { ["title"] = siteText.Title, ["description"] = siteText.Description, ["url"] = settings.BaseUrl,
            ["avatar"] = string.IsNullOrEmpty(settings.AvatarUrl) ? ShortLinks.Avatar(settings.AvatarUserName) : settings.AvatarUrl };
        data["language"] = UiText.Language;
        data["client_messages"] = UiText.ClientJson;
        data["languages"] = UiText.Enabled(settings).Select(c => new Dictionary<string, object?> { ["name"] = c.NativeName, ["code"] = c.Name, ["selected"] = c.Name == UiText.Language,
            ["url"] = "/language?culture=" + Uri.EscapeDataString(c.Name) + "&returnUrl=" + Uri.EscapeDataString(http.Request.Path + http.Request.QueryString) }).ToArray();
        data["language_return"] = http.Request.Path + http.Request.QueryString;
        data["feed_url"] = "/feed.xml?culture=" + UiText.Language;
        data["registration_open"] = settings.Registration != RegistrationMode.Closed;
        data["user_url"] = PublicUrls.Author(http.User.Identity?.Name ?? "guest");
        data["user_avatar"] = ShortLinks.Avatar(http.User.Identity?.Name ?? "guest");
        data["navigation"] = navigation.Where(n => n.ParentId == null).Select(n => new Dictionary<string, object?> {
            ["text"] = n.Text, ["url"] = n.Url, ["new_window"] = OpensNewWindow(n.Url), ["children"] = navigation.Where(child => child.ParentId == n.Id)
                .Select(child => new Dictionary<string, object?> { ["text"] = child.Text, ["url"] = child.Url, ["new_window"] = OpensNewWindow(child.Url) }).ToArray()
        }).ToArray();
        if (!data.ContainsKey("categories"))
        {
            var categories = await db.Site.Select<Category>().OrderBy(c => c.Name).ToListAsync();
            data["categories"] = categories.Select(c => new Dictionary<string, object?> { ["name"] = c.Name, ["url"] = PublicUrls.Category(c.Name) }).ToArray();
        }
        var recent = await db.Site.Select<Post, PostRevision>().InnerJoin((p, r) => p.PublishedRevisionId == r.Id)
            .Where((p, r) => p.IsPublic && p.Kind == PostKind.Article)
            .OrderByDescending((p, r) => p.PublishedUtc).OrderByDescending((p, r) => p.Id).Limit(5)
            .ToListAsync((p, r) => new { Post = p, Revision = r });
        data["recent_posts"] = recent.Select(p => ArticleLink(p.Post, p.Revision, settings)).ToArray();
        var featured = await db.Site.Select<Post, PostRevision>().InnerJoin((p, r) => p.PublishedRevisionId == r.Id)
            .Where((p, r) => p.IsPublic && p.Kind == PostKind.Article && p.PinOrder > 0)
            .OrderByDescending((p, r) => p.PinOrder).OrderByDescending((p, r) => p.PublishedUtc).OrderByDescending((p, r) => p.Id)
            .Limit(5).ToListAsync((p, r) => new { Post = p, Revision = r });
        data["featured_posts"] = featured.Select(p => ArticleLink(p.Post, p.Revision, settings)).ToArray();
        data["site_stats"] = new Dictionary<string, object?> {
            ["posts"] = await db.Site.Select<Post, PostRevision>().InnerJoin((p, r) => p.PublishedRevisionId == r.Id)
                .Where((p, r) => p.IsPublic && p.Kind == PostKind.Article).CountAsync(),
            ["comments"] = await db.Site.Select<Comment, Post, PostRevision>().InnerJoin((c, p, r) => c.PostId == p.Id).InnerJoin((c, p, r) => p.PublishedRevisionId == r.Id)
                .Where((c, p, r) => c.Approved && p.IsPublic && p.Kind == PostKind.Article).CountAsync(),
            ["tags"] = await db.Site.Select<Tag>().Where(t => db.Site.Select<RevisionTag, Post>().InnerJoin((rt, p) => rt.RevisionId == p.PublishedRevisionId)
                .Where((rt, p) => rt.TagId == t.Id && p.IsPublic && p.Kind == PostKind.Article).Any()).CountAsync(),
            ["latest_post"] = recent.FirstOrDefault()?.Post.PublishedUtc?.ToString("yyyy-MM-dd") ?? "",
            ["latest_post_utc"] = PublicUrls.Utc(recent.FirstOrDefault()?.Post.PublishedUtc)
        };
        data["signed_in"] = http.User.Identity?.IsAuthenticated == true;
        data["can_manage"] = AccountService.CanEditAll(http.User) || http.User.IsInRole("Contributor");
        data["user_name"] = http.User.Identity?.Name ?? "";
        data["csrf"] = antiforgery.GetAndStoreTokens(http).RequestToken;
        data.TryAdd("title", siteText.Title);
        data.TryAdd("description", siteText.Description);
        data.TryAdd("canonical", settings.BaseUrl + http.Request.Path);
        http.Response.Headers.CacheControl = "no-store";
        return new ContentResult { Content = await RenderAsync(settings.Theme, template, data), ContentType = "text/html; charset=utf-8", StatusCode = status };
    }

    private static bool OpensNewWindow(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ||
        url.Split('?', '#')[0] is "/feed.xml" or "/sitemap.xml";

    internal static Dictionary<string, object?> ArticleLink(Post post, PostRevision revision, SiteSettings settings)
    {
        var text = ContentLanguages.Display(revision, settings);
        return new() { ["title"] = text.Title, ["summary"] = text.Summary, ["url"] = "/" + post.Slug,
            ["cover"] = text.CoverUrl, ["date"] = post.PublishedUtc?.ToString("yyyy-MM-dd"), ["date_utc"] = PublicUrls.Utc(post.PublishedUtc) };
    }

    public void InitializeDefault()
    {
        foreach (var id in new[] { "default", "milky" })
        {
            var source = Path.Combine(env.ContentRootPath, "theme", id);
            var destination = Path.Combine(paths.Themes, id);
            // Both bundled themes are release-managed. Uploaded themes use separate IDs.
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(destination, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, true);
            }
        }
    }

    public async Task<string> RenderAsync(string theme, string template, Dictionary<string, object?> data)
    {
        if (!Regex.IsMatch(theme, "^[a-z0-9][a-z0-9-]{0,63}$") || !(Templates.Contains(template) || template == "page.liquid"))
            throw new InvalidDataException(UiText.T("主题或模板名称无效。"));
        var options = new TemplateOptions { MaxRecursion = 32, MaxSteps = 100000 };
        options.Filters.AddFilter("t", (input, arguments, context) => new ValueTask<FluidValue>(new StringValue(UiText.T(input.ToStringValue()))));
        var context = new TemplateContext(options);
        foreach (var item in data) context.SetValue(item.Key, item.Value);
        context.SetValue("theme_url", "/theme-assets/" + theme);
        foreach (var file in new[] { template, "layout.liquid" })
        {
            var path = Path.Combine(paths.Themes, theme, file);
            if (file == "page.liquid" && !File.Exists(path)) path = Path.Combine(paths.Themes, "default", file);
            var source = await File.ReadAllTextAsync(path);
            if (!parser.TryParse(source, out var parsed, out var error)) throw new InvalidDataException(UiText.T("主题模板解析失败：") + error);
            var html = await parsed.RenderAsync(context, HtmlEncoder.Default);
            if (file == "layout.liquid") return html;
            context.SetValue("body", html);
        }
        return "";
    }

    public async Task InstallAsync(IFormFile upload)
    {
        if (upload.Length == 0 || upload.Length > 20 * 1024 * 1024) throw new InvalidDataException(UiText.T("主题 ZIP 最大 20 MB。"));
        using var zip = new ZipArchive(upload.OpenReadStream(), ZipArchiveMode.Read);
        if (zip.Entries.Count > 200 || zip.Entries.Sum(e => e.Length) > 50 * 1024 * 1024)
            throw new InvalidDataException(UiText.T("主题最多 200 个文件，解压后最大 50 MB。"));
        var manifestEntry = zip.GetEntry("theme.json") ?? throw new InvalidDataException(UiText.T("ZIP 根目录缺少 theme.json。"));
        using var manifestStream = manifestEntry.Open();
        var manifest = await JsonSerializer.DeserializeAsync<ThemeManifest>(manifestStream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (manifest == null || !Regex.IsMatch(manifest.Id, "^[a-z0-9][a-z0-9-]{0,63}$") || manifest.Id is "default" or "milky" ||
            manifest.Version != 1 || string.IsNullOrWhiteSpace(manifest.Name) || manifest.Name.Length > 100)
            throw new InvalidDataException(UiText.T("主题清单无效，或试图覆盖内置主题。"));
        var destination = Path.Combine(paths.Themes, manifest.Id);
        if (Directory.Exists(destination)) throw new InvalidDataException(UiText.T("同名主题已存在。请使用新的主题标识安装新版本。"));
        foreach (var template in Templates.Append("page.liquid"))
        {
            var entry = zip.GetEntry(template);
            if (entry == null && template == "page.liquid") continue;
            if (entry == null) throw new InvalidDataException(UiText.T("缺少模板：") + template);
            if (entry.Length > 256 * 1024) throw new InvalidDataException(UiText.T("单个模板最大 256 KB。"));
            using var reader = new StreamReader(entry.Open());
            if (!parser.TryParse(await reader.ReadToEndAsync(), out _, out var error)) throw new InvalidDataException(UiText.T("模板无效：") + error);
        }
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            var name = entry.FullName;
            if (name.EndsWith('/')) continue;
            if (!names.Add(name) || name.Contains('\\') || name.Split('/').Any(s => s is ".." or "." or "") ||
                !Regex.IsMatch(name, "^[a-zA-Z0-9_./-]+$") ||
                !(name == "theme.json" || Templates.Contains(name) || name == "page.liquid" || name.StartsWith("assets/", StringComparison.Ordinal) &&
                  new[] { ".css", ".js", ".png", ".jpg", ".jpeg", ".webp", ".gif", ".woff2" }.Contains(Path.GetExtension(name))) ||
                (entry.ExternalAttributes >> 16 & 0xF000) == 0xA000)
                throw new InvalidDataException(UiText.T("主题包含非法路径、重复文件或不支持的文件类型。"));
        }
        var staging = Path.Combine(paths.Root, "theme-upload-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            foreach (var entry in zip.Entries.Where(e => !e.FullName.EndsWith('/')))
            {
                var target = Path.Combine(staging, entry.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await using var output = File.Create(target);
                await using var input = entry.Open();
                await input.CopyToAsync(output);
            }
            Directory.Move(staging, destination);
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }
}

public class ThemeManifest
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Version { get; set; }
}
