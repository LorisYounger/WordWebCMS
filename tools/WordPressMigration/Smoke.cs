using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;

static class Smoke
{
    public static async Task Run(MigrationConfig config)
    {
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() };
        using var client = new HttpClient(handler) { BaseAddress = new Uri(config.BaseUrl), Timeout = TimeSpan.FromSeconds(20) };
        var parser = new HtmlParser();
        var checks = new List<string>();
        void Check(bool passed, string name) { if (!passed) throw new InvalidDataException("HTTP verification failed: " + name); checks.Add(name); }
        using var snapshot = JsonDocument.Parse(File.ReadAllText(Path.Combine(config.OutputPath, "source-snapshot.json")));
        var published = snapshot.RootElement.GetProperty("posts").EnumerateArray().Where(p => p.GetProperty("post_type").GetString() == "post" && p.GetProperty("post_status").GetString() == "publish").ToArray();
        using (var response = await client.GetAsync("/")) {
            var html = await response.Content.ReadAsStringAsync();
            var page = parser.ParseDocument(html);
            Check(response.StatusCode == HttpStatusCode.OK && page.Title?.Contains("L版公司") == true, "home and Chinese site title");
            Check(page.QuerySelectorAll("a").Any(a => a.GetAttribute("href") == "https://beian.miit.gov.cn" && a.TextContent.Contains("粤ICP备2024264619号")), "original ICP footer");
            foreach (var post in published) Check(page.QuerySelectorAll("a").Any(a => a.GetAttribute("href") == "/" + post.GetProperty("post_name").GetString()), "home link " + post.GetProperty("post_name").GetString());
        }
        foreach (var post in published) {
            var slug = post.GetProperty("post_name").GetString()!;
            using var response = await client.GetAsync("/" + slug);
            var page = parser.ParseDocument(await response.Content.ReadAsStringAsync());
            Check(response.StatusCode == HttpStatusCode.OK && page.Title?.Contains(post.GetProperty("post_title").GetString()!) == true, "article " + slug);
            if (slug == "sendkeys") Check(page.QuerySelectorAll("main table").Length == 2 && page.QuerySelectorAll("main table tr").Length >= 40, "both SendKeys tables and their rows");
            if (slug == "vpet") Check(page.QuerySelectorAll("main img").Count(i => (i.GetAttribute("src") ?? "").StartsWith("https://static.exlb.net/")) == 3, "VPet external GIF links retained");
            using var trailing = await client.GetAsync("/" + slug + "/");
            Check(trailing.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.PermanentRedirect && trailing.Headers.Location?.OriginalString == "/" + slug, "trailing slash redirect " + slug);
        }
        foreach (var path in new[] { "/feed.xml", "/sitemap.xml", "/?q=" + Uri.EscapeDataString("虚拟桌宠"), "/?category=4", "/?tag=4", "/theme-assets/lbnet/assets/site.css" }) {
            using var response = await client.GetAsync(path);
            Check(response.StatusCode == HttpStatusCode.OK, "public endpoint " + path);
        }
        foreach (var path in new[] { "/privacy-policy", "/App_Data/setup.json", "/App_Data/mail.json", "/Migration-private/admin-credentials.private.json", "/Migration-private/source-snapshot.json" }) {
            using var response = await client.GetAsync(path);
            Check(response.StatusCode == HttpStatusCode.NotFound, "private/unpublished path " + path);
        }
        using (var setup = await client.GetAsync("/Setup")) Check(setup.StatusCode == HttpStatusCode.Found, "setup locked after migration");
        var media = JsonSerializer.Deserialize<List<Migration.MediaEntry>>(File.ReadAllText(Path.Combine(config.OutputPath, "media-manifest.json")))!;
        foreach (var file in media) {
            using var response = await client.GetAsync("/media/" + file.Target);
            var data = await response.Content.ReadAsByteArrayAsync();
            Check(response.StatusCode == HttpStatusCode.OK && response.Content.Headers.ContentType?.MediaType == "image/png" && Convert.ToHexString(SHA256.HashData(data)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase), "media bytes " + file.Source);
        }
        using var credentials = JsonDocument.Parse(File.ReadAllText(Path.Combine(config.OutputPath, "admin-credentials.private.json")));
        var login = parser.ParseDocument(await client.GetStringAsync("/Account/Login"));
        var form = login.QuerySelectorAll("input[type=hidden][name]").ToDictionary(e => e.GetAttribute("name")!, e => e.GetAttribute("value") ?? "");
        var math = Regex.Match(login.QuerySelector("label[for=ChallengeAnswer]")!.TextContent, @"(\d+)\s*\+\s*(\d+)");
        form["ChallengeAnswer"] = (int.Parse(math.Groups[1].Value) + int.Parse(math.Groups[2].Value)).ToString();
        form["Login"] = credentials.RootElement.GetProperty("UserName").GetString()!;
        form["Password"] = credentials.RootElement.GetProperty("Password").GetString()!;
        using (var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(form))) Check(response.StatusCode == HttpStatusCode.Found, "admin login with antiforgery and arithmetic challenge");
        foreach (var path in new[] { "/Admin", "/Admin/Media", "/Admin/Categories", "/Admin/Navigation", "/Admin/Settings", "/Admin/Comments", "/Admin/Users" }) {
            using var response = await client.GetAsync(path);
            Check(response.StatusCode == HttpStatusCode.OK, "authenticated " + path);
        }
        var map = JsonSerializer.Deserialize<Migration.ImportMap>(File.ReadAllText(Path.Combine(config.OutputPath, "import-map.json")))!;
        foreach (var pair in map.Posts) {
            using var response = await client.GetAsync("/Admin/Edit/" + pair.Value);
            Check(response.StatusCode == HttpStatusCode.OK, "edit and history screen for source " + pair.Key);
        }
        var result = new { Passed = true, Count = checks.Count, Checks = checks, CheckedUtc = DateTime.UtcNow };
        File.WriteAllText(Path.Combine(config.OutputPath, "http-verification.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"PASS: {checks.Count} HTTP checks, including admin login, 7 public articles, draft privacy, media hashes and private-file isolation.");
    }
}
