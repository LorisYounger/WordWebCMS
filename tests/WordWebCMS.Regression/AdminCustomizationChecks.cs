using System.Net;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Identity;
using WordWebCMS.Data;
using WordWebCMS.Models;
using WordWebCMS.Services;

internal static class AdminCustomizationChecks
{
    public static async Task RunAsync(CmsDb db, HttpClient admin, HttpClient visitor, CmsUser owner, string password, Action<bool, string> check)
    {
        var parser = new HtmlParser();
        async Task<AngleSharp.Dom.IDocument> Document(string path, HttpClient? client = null) => parser.ParseDocument(await (client ?? visitor).GetStringAsync(path));
        async Task<string> Token(string path, HttpClient? client = null) => (await Document(path, client ?? admin)).QuerySelector("input[name=__RequestVerificationToken]")!.GetAttribute("value")!;
        async Task<HttpResponseMessage> Post(string path, Dictionary<string, string> form, HttpClient? client = null) => await (client ?? admin).PostAsync(path, new FormUrlEncodedContent(form));
        var original = db.Site.Select<SiteSettings>().First();
        var translations = ContentLanguages.Read<SiteTranslation>(original.Translations);
        translations["en"] = new SiteTranslation { FooterMarkdown = "English footer [Contact](/contact)" };
        var form = new Dictionary<string, string> {
            ["__RequestVerificationToken"] = await Token("/Admin/Settings"), ["Settings.Title"] = original.Title,
            ["Settings.BaseUrl"] = original.BaseUrl, ["Settings.Description"] = original.Description,
            ["Settings.AvatarUrl"] = original.AvatarUrl, ["Settings.Language"] = "zh-Hans", ["SelectedLanguages"] = "zh-Hans",
            ["Settings.Registration"] = original.Registration.ToString(), ["Settings.HideUserAvatars"] = "true",
            ["Settings.FooterMarkdown"] = "版权所有 **测试站点** [备案信息](https://example.test/license) <script>alert(1)</script>",
            ["Translations[en].FooterMarkdown"] = translations["en"].FooterMarkdown
        };
        try
        {
            check((await Post("/Admin/Settings", form)).StatusCode == HttpStatusCode.Redirect, "Admin saves avatar visibility and translated footer");
            var saved = db.Site.Select<SiteSettings>().First();
            check(saved.HideUserAvatars && saved.FooterMarkdown.Contains("版权所有") && ContentLanguages.Read<SiteTranslation>(saved.Translations)["en"].FooterMarkdown.Contains("English footer"), "New site settings persist in database");
            db.Site.Update<SiteSettings>().Set(s => s.EnabledLanguages, "zh-Hans,en,fr-FR").Where(s => s.Id == 1).ExecuteAffrows();
            foreach (var theme in new[] { "default", "milky" })
            {
                db.Site.Update<SiteSettings>().Set(s => s.Theme, theme).Where(s => s.Id == 1).ExecuteAffrows();
                foreach (var path in new[] { "/?culture=zh-Hans", "/feature-article-0?culture=zh-Hans" })
                {
                    var doc = await Document(path, admin);
                    check(doc.QuerySelectorAll(".author-link img").Length == 0 && doc.QuerySelectorAll(".author-link").Length > 0, theme + " hides avatars while retaining author links: " + path);
                    check(doc.QuerySelector(".footer-content strong")?.TextContent == "测试站点" && doc.QuerySelector(".footer-content a")?.GetAttribute("href") == "https://example.test/license" && doc.QuerySelector(".footer-content script") == null, theme + " renders sanitized Markdown footer");
                }
                check((await Document("/?culture=en")).QuerySelector(".footer-content")!.TextContent.Contains("English footer"), theme + " selects English footer");
                check((await Document("/?culture=fr-FR")).QuerySelector(".footer-content")!.TextContent.Contains("English footer"), theme + " falls back to English footer");
                check((await Document(PublicUrls.Author(owner.UserName))).QuerySelector(".profile-card img") != null, theme + " retains the profile avatar");
                db.Site.Update<SiteSettings>().Set(s => s.HideUserAvatars, false).Set(s => s.FooterMarkdown, "").Where(s => s.Id == 1).ExecuteAffrows();
                var visible = await Document("/feature-article-0?culture=zh-Hans");
                check(visible.QuerySelector(".article-header .author-link img") != null && visible.QuerySelector(".footer-content") == null, theme + " restores avatars and default footer");
                db.Site.Update<SiteSettings>().Set(s => s.HideUserAvatars, true).Set(s => s.FooterMarkdown, saved.FooterMarkdown).Where(s => s.Id == 1).ExecuteAffrows();
            }
            form["Settings.FooterMarkdown"] = new string('x', 10001);
            check((await Post("/Admin/Settings", form)).StatusCode == HttpStatusCode.Conflict && db.Site.Select<SiteSettings>().First().FooterMarkdown == saved.FooterMarkdown, "Oversize footer rejected without partial save");
            form["Settings.FooterMarkdown"] = "valid"; form["Translations[en].FooterMarkdown"] = new string('x', 10001);
            check((await Post("/Admin/Settings", form)).StatusCode == HttpStatusCode.Conflict, "Oversize footer translation rejected");
        }
        finally { db.Site.Update<SiteSettings>().SetSource(original).ExecuteAffrows(); }

        var accounts = new AccountService(db, new PasswordHasher<CmsUser>());
        var member = await accounts.CreateAsync("management-member", "management-member@example.test", password, true);
        db.Site.Insert(new SiteUserRole { UserId = member.Id, Role = SiteRole.Reader }).ExecuteAffrows();
        using var memberClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = visitor.BaseAddress };
        try
        {
            async Task<HttpResponseMessage> Login()
            {
                var doc = await Document("/Account/Login?culture=en", memberClient);
                var challenge = Regex.Match(doc.QuerySelector("label[for=ChallengeAnswer]")!.TextContent, @"(\d+) \+ (\d+)");
                var login = doc.QuerySelectorAll("input[type=hidden][name]").ToDictionary(e => e.GetAttribute("name")!, e => e.GetAttribute("value") ?? "");
                login["Login"] = member.UserName; login["Password"] = password;
                login["ChallengeAnswer"] = (int.Parse(challenge.Groups[1].Value) + int.Parse(challenge.Groups[2].Value)).ToString();
                return await Post("/Account/Login?culture=en", login, memberClient);
            }
            check((await Login()).StatusCode == HttpStatusCode.Redirect, "Managed user can initially sign in");
            var search = await Document("/Admin/Users?Q=management-member&Status=Reader", admin);
            check(search.QuerySelectorAll(".user-row").Length == 1 && search.QuerySelector(".user-row")!.TextContent.Contains(member.Email), "User search combines account fields and site role filter");
            var remove = new Dictionary<string, string> { ["__RequestVerificationToken"] = await Token("/Admin/Users"), ["userId"] = member.Id.ToString() };
            check((await Post("/Admin/Users?handler=Remove", new() { ["userId"] = member.Id.ToString() })).StatusCode == HttpStatusCode.BadRequest, "Removing users requires CSRF protection");
            var readerRemove = await Post("/Admin/Users?handler=Remove", remove, memberClient);
            check(readerRemove.StatusCode == HttpStatusCode.Redirect && readerRemove.Headers.Location?.ToString().Contains("/Error/403") == true &&
                db.Site.Select<SiteUserRole>().Where(r => r.UserId == member.Id).First().Role == SiteRole.Reader, "Readers cannot remove users");
            remove["userId"] = owner.Id.ToString();
            check((await Post("/Admin/Users?handler=Remove", remove)).StatusCode == HttpStatusCode.Conflict, "Administrator cannot remove their own account");
            remove["userId"] = member.Id.ToString();
            check((await Post("/Admin/Users?handler=Remove", remove)).StatusCode == HttpStatusCode.Redirect && db.Site.Select<SiteUserRole>().Where(r => r.UserId == member.Id).First().Role == SiteRole.Removed, "Removing a user records the site-local removal");
            check(db.Accounts.Select<CmsUser>().Where(u => u.Id == member.Id && !u.Disabled).Any(), "Removal preserves the active shared account");
            check((await memberClient.GetAsync("/Account/Profile")).StatusCode == HttpStatusCode.Redirect, "Removing user immediately invalidates existing session");
            db.Site.Update<SiteSettings>().Set(s => s.Registration, RegistrationMode.Open).Where(s => s.Id == 1).ExecuteAffrows();
            check((await Login()).StatusCode == HttpStatusCode.Forbidden, "Removed user cannot rejoin through open registration");
            check((await Document("/Admin/Users?Q=management-member", admin)).QuerySelectorAll(".user-row").Length == 0 &&
                (await Document("/Admin/Users?Q=management-member&Status=Removed", admin)).QuerySelectorAll(".user-row").Length == 1, "Removed users are hidden by default and available in Removed filter");
            var restore = new Dictionary<string, string> { ["__RequestVerificationToken"] = await Token("/Admin/Users"), ["login"] = member.UserName, ["role"] = "Reader" };
            check((await Post("/Admin/Users", restore)).StatusCode == HttpStatusCode.Redirect && (await Login()).StatusCode == HttpStatusCode.Redirect, "Administrator can restore a removed user's access");
        }
        finally
        {
            db.Site.Update<SiteSettings>().SetSource(original).ExecuteAffrows();
            db.Site.Delete<SiteUserRole>().Where(r => r.UserId == member.Id).ExecuteAffrows();
            db.Accounts.Delete<CmsUser>().Where(u => u.Id == member.Id).ExecuteAffrows();
        }
    }
}
