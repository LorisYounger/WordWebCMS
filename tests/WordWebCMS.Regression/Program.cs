using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using WordWebCMS.Data;
using WordWebCMS.Models;
using WordWebCMS.Services;

var root = Path.GetFullPath(args.FirstOrDefault() ?? ".");
if (args.Contains("--cache-only"))
{
    await CacheLocalChecks.RunAsync(root);
    return;
}
var config = new ConfigurationBuilder().AddJsonFile(Path.Combine(root, "Local/appsettings.local.json"), optional: true).AddEnvironmentVariables().Build();
var database = "wwcms_test_" + Guid.NewGuid().ToString("N");
var connection = new MySqlConnectionStringBuilder(config.GetConnectionString("Site") ?? throw new InvalidOperationException("Configure a test MySQL connection.")) { Database = "" };
using var server = new MySqlConnection(connection.ConnectionString);
for (var attempt = 0; ; attempt++)
{
    try { await server.OpenAsync(); break; }
    catch (MySqlException) when (attempt < 30) { await Task.Delay(1000); }
}
await new MySqlCommand($"CREATE DATABASE `{database}` CHARACTER SET utf8mb4", server).ExecuteNonQueryAsync();
connection.Database = database;
var storage = Path.Combine(root, "artifacts", database);
Directory.CreateDirectory(Path.Combine(storage, "languages"));
await File.WriteAllTextAsync(Path.Combine(storage, "languages/fr-FR.json"), "{\"首页\":\"Accueil\",\"登录\":\"Connexion\"}");
var settings = new Dictionary<string, string?> { ["ConnectionStrings:Site"] = connection.ConnectionString, ["ConnectionStrings:Accounts"] = connection.ConnectionString };
using var db = new CmsDb(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
using var smtp = new TestSmtp();
Process? web = null;
var logs = new ConcurrentQueue<string>();
int passed = 0;
void Check(bool result, string name) { if (!result) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); passed++; }
var parser = new HtmlParser();
var password = "Regression-" + Guid.NewGuid().ToString("N");
var baseUrl = "http://127.0.0.1:5193";
HttpClient Client() => new(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(15) };
async Task<string> Get(HttpClient client, string path) => WebUtility.HtmlDecode(await client.GetStringAsync(path));
async Task<Dictionary<string, string>> Form(HttpClient client, string path, bool solve = false)
{
    var html = await Get(client, path);
    var document = parser.ParseDocument(html);
    var data = document.QuerySelectorAll("input[type=hidden][name]").GroupBy(e => e.GetAttribute("name")!)
        .ToDictionary(g => g.Key, g => g.First().GetAttribute("value") ?? "");
    if (solve)
    {
        var match = Regex.Match(document.QuerySelector("label[for=ChallengeAnswer]")!.TextContent, @"(\d+) \+ (\d+)");
        data["ChallengeAnswer"] = (int.Parse(match.Groups[1].Value) + int.Parse(match.Groups[2].Value)).ToString();
    }
    return data;
}
async Task<HttpResponseMessage> Send(HttpClient client, string path, Dictionary<string, string> data) => await client.PostAsync(path, new FormUrlEncodedContent(data));
ClaimsPrincipal Principal(long id) => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, "Administrator")], "test"));
try
{
    await SetupChecks.RunAsync(root, connection.ConnectionString, Check);
    db.InitializeSchema();
    Check(db.CheckSchema(), "Schema v4 initializes");
    var accounts = new AccountService(db, new PasswordHasher<CmsUser>());
    var admin = await accounts.CreateAsync("regression-admin", "admin@example.test", password, true);
    db.Site.Insert(new SiteSettings { Title = "Regression site", BaseUrl = baseUrl, AvatarUserName = admin.UserName, EnabledLanguages = "zh-CN,en,fr-FR" }).ExecuteAffrows();
    db.Site.Insert(new SiteUserRole { UserId = admin.Id, Role = SiteRole.Administrator }).ExecuteAffrows();
    // Simulate the owning v1 settings schema, then perform the actual migration.
    db.Site.Ado.ExecuteNonQuery("ALTER TABLE site_settings DROP COLUMN AvatarUrl, DROP COLUMN AvatarUserName, DROP COLUMN Language, DROP COLUMN Registration");
    db.Site.Update<SchemaVersion>().Set(v => v.Version, 1).Where(v => v.Id == 1).ExecuteAffrows();
    using (var migration = new CmsDb(new ConfigurationBuilder().AddInMemoryCollection(settings).Build())) migration.InitializeSchema();
    Check(db.CheckSchema() && db.Site.Select<SiteSettings>().First().Title == "Regression site", "v1 migration preserves site data");
    Check(db.Site.Select<SiteSettings>().First().Language == "zh-Hans", "Migrated site has a default culture");
    Check(db.Site.Select<SiteSettings>().First().AvatarUserName == admin.UserName, "Migrated avatar uses site administrator");

    db.Accounts.Ado.ExecuteNonQuery("ALTER TABLE cms_user DROP COLUMN Bio");
    db.Site.Update<SchemaVersion>().Set(v => v.Version, 3).Where(v => v.Id == 1).ExecuteAffrows();
    using (var migration = new CmsDb(new ConfigurationBuilder().AddInMemoryCollection(settings).Build()))
    {
        migration.InitializeSchema();
        migration.InitializeSchema();
        Check(migration.CheckSchema() && migration.Accounts.Select<CmsUser>().Where(u => u.Id == admin.Id).First().PasswordHash == admin.PasswordHash, "v3 to v4 migration preserves shared accounts and is repeatable");
    }

    UiText.Load(Path.Combine(root, "WordWebCMS/Languages"), Path.Combine(storage, "languages"));
    var mailConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
        ["Mail:Host"] = "127.0.0.1", ["Mail:Port"] = smtp.Port.ToString(), ["Mail:Security"] = "None",
        ["Mail:From"] = "fallback@example.test", ["Mail:Password"] = "deployment-password"
    }).Build();
    var mailStorage = Path.Combine(storage, "mail-service-tests");
    Directory.CreateDirectory(mailStorage);
    var mailKeys = new DirectoryInfo(Path.Combine(mailStorage, "keys"));
    var mailService = new MailService(mailConfig, new StoragePaths(mailStorage), DataProtectionProvider.Create(mailKeys));
    Check(mailService.Configured && mailService.ReadSettings().From == "fallback@example.test", "Mail uses deployment settings before the first admin save");
    var mailSettings = mailService.ReadSettings();
    mailSettings.Password = "";
    mailService.SaveSettings(mailSettings, false);
    Check(mailService.ReadSettings().Password == "deployment-password", "First admin save with blank password preserves deployment password");
    mailSettings.Password = "new-private-password";
    mailService.SaveSettings(mailSettings, false);
    Check(!File.ReadAllText(Path.Combine(mailStorage, "mail.json")).Contains("new-private-password"), "SMTP password is encrypted at rest");
    var reloadedMail = new MailService(mailConfig, new StoragePaths(mailStorage), DataProtectionProvider.Create(mailKeys));
    Check(reloadedMail.ReadSettings().Password == "new-private-password", "Persisted SMTP credentials survive service recreation with the same keys");
    mailService.SaveSettings(mailSettings, false);
    Check(reloadedMail.ReadSettings().Password == "new-private-password", "Blank SMTP password preserves the current password");
    mailService.SaveSettings(mailSettings, true);
    Check(reloadedMail.ReadSettings().Password == "", "Explicit password clearing does not fall back to deployment credentials");
    mailSettings.Host = ""; mailSettings.From = "";
    mailService.SaveSettings(mailSettings, false);
    Check(!reloadedMail.Configured, "An explicitly disabled mail configuration overrides deployment settings");
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
    Check(UiText.T("登录") == "Sign in", "CultureInfo parent language fallback");
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
    Check(UiText.T("登录") == "Connexion" && UiText.T("未翻译") == "未翻译", "Custom language and missing-key fallback");
    Check(UiText.T("最新文章") == "Latest posts", "Missing non-Chinese UI text uses English");
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-TW");
    Check(UiText.T("登录") == "登录", "Missing Chinese UI text uses Simplified Chinese");
    var fallbackSettings = new SiteSettings { Language = "de", Title = "Default site", Description = "Default description",
        Translations = ContentLanguages.Write(new Dictionary<string, SiteTranslation> { ["en"] = new() { Title = "English site" }, ["zh-Hans"] = new() { Title = "简体站点" } }) };
    var fallbackContent = new PostDraft { Title = "Default title", Summary = "Default summary", Markdown = "Default body", CoverUrl = "/default.png",
        Translations = ContentLanguages.Write(new Dictionary<string, ContentTranslation> {
            ["en"] = new() { Title = "English title", Markdown = "English body" },
            ["zh-CN"] = new() { Title = "简体标题", Markdown = "简体正文" },
            ["zh-TW"] = new() { Title = "繁體標題" },
            ["fr-FR"] = new() { Title = "Titre français", Summary = " " },
            ["de"] = new() { Title = "Stale duplicate default" }
        }) };
    var fallbackText = ContentLanguages.Display(fallbackContent, fallbackSettings);
    Check(fallbackText.Title == "繁體標題" && fallbackText.Markdown == "简体正文" && fallbackText.CoverUrl == "/default.png", "Chinese partial translations use Simplified Chinese, then default fields");
    Check(ContentLanguages.Display(fallbackSettings).Title == "简体站点", "Chinese site metadata uses Simplified Chinese fallback");
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
    fallbackText = ContentLanguages.Display(fallbackContent, fallbackSettings);
    Check(fallbackText.Title == "Titre français" && fallbackText.Markdown == "English body" && fallbackText.Summary == "Default summary", "Non-Chinese partial translations use English, then default fields");
    Check(ContentLanguages.Display(fallbackSettings).Title == "English site", "Non-Chinese site metadata uses English fallback");
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de");
    Check(ContentLanguages.Display(fallbackContent, fallbackSettings).Title == "Default title", "Default language uses base content without a duplicate translation");
    Check(UiText.Enabled(new SiteSettings { Language = "zh-CN", EnabledLanguages = "zh-CN,en" }).Any(c => c.Name == "zh-Hans"), "Existing Simplified Chinese settings resolve to zh-Hans");
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");

    using var cache = new MemoryCache(new MemoryCacheOptions());
    var math = new MathChallenge(cache);
    var browser = new string('a', 48);
    var question = math.Create(browser);
    var numbers = Regex.Matches(question.Question, @"\d+").Select(m => int.Parse(m.Value)).Sum().ToString();
    Check(math.Verify(browser, question.Token, numbers), "Arithmetic accepts the correct answer");
    Check(!math.Verify(browser, question.Token, numbers), "Arithmetic rejects replay");
    question = math.Create(browser);
    Check(!math.Verify(new string('b', 48), question.Token, "1"), "Arithmetic is bound to browser");
    question = math.Create(browser);
    cache.Remove("math:" + question.Token);
    Check(!math.Verify(browser, question.Token, "1"), "Missing or expired challenge is rejected");

    var content = new ContentService(db);
    long Publish(PostKind kind, string title, string slug = "")
    {
        var id = content.Create(kind, Principal(admin.Id));
        var draft = db.Site.Select<PostDraft>().Where(d => d.PostId == id).First();
        Check(draft.Slug == "", "New draft has no random short link");
        draft.Title = title; draft.Slug = slug; draft.Markdown = "## Introduction\nPage or post body.";
        draft.AllowComments = true;
        content.Save(id, draft, Principal(admin.Id), "publish");
        return id;
    }
    var articleId = Publish(PostKind.Article, "Welcome", "welcome");
    var pageId = Publish(PostKind.Page, "About", "about");
    var englishId = Publish(PostKind.Article, "My First English Title!");
    Check(db.Site.Select<Post>().Where(p => p.Id == englishId).First().Slug == "my-first-english-title", "English title generates readable short link");
    var duplicateId = Publish(PostKind.Article, "My First English Title!");
    Check(db.Site.Select<Post>().Where(p => p.Id == duplicateId).First().Slug == "my-first-english-title-2", "Automatic short links resolve collisions");
    var chineseId = Publish(PostKind.Page, "介绍");
    Check(db.Site.Select<Post>().Where(p => p.Id == chineseId).First().Slug == "new-page-" + chineseId, "Non-Latin title has readable fallback");
    Check(!db.Site.Select<PostDraft>().Where(d => d.PostId == pageId).First().AllowComments, "Pages force comments off when saving");
    foreach (var invalid in new[] { "admin", "account", "setup", "language", "media", "theme-assets", "a/b", "//example.com" })
        Check(!ShortLinks.IsValid(invalid), "Reject reserved or malformed short link: " + invalid);
    var collision = db.Site.Select<PostDraft>().Where(d => d.PostId == pageId).First();
    collision.Slug = "welcome";
    try { content.Save(pageId, collision, Principal(admin.Id), "publish"); Check(false, "Explicit collision"); }
    catch (InvalidDataException) { Check(true, "Explicit collision rejected without replacing publication"); }

    var start = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.Combine(root, "WordWebCMS"), UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
    start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "WordWebCMS.dll"));
    start.ArgumentList.Add("--urls"); start.ArgumentList.Add(baseUrl);
    foreach (var entry in settings) start.Environment[entry.Key.Replace(":", "__")] = entry.Value;
    start.Environment["Storage__Path"] = storage;
    start.Environment["Site__CookieName"] = database;
    start.Environment["ASPNETCORE_ENVIRONMENT"] = OperatingSystem.IsWindows() ? "Development" : "Production";
    start.Environment["Mail__Host"] = ""; start.Environment["Mail__From"] = "";
    start.Environment["Mail__User"] = ""; start.Environment["Mail__Password"] = "";
    web = Process.Start(start)!;
    web.OutputDataReceived += (_, e) => { if (e.Data != null) logs.Enqueue(e.Data); };
    web.ErrorDataReceived += (_, e) => { if (e.Data != null) logs.Enqueue(e.Data); };
    web.BeginOutputReadLine(); web.BeginErrorReadLine();
    using var visitor = Client();
    for (var i = 0; ; i++)
    {
        try { await visitor.GetAsync("/"); break; }
        catch (HttpRequestException) when (i < 50 && !web.HasExited) { await Task.Delay(200); }
    }
    var home = await Get(visitor, "/");
    Check(home.Contains("/welcome") && !home.Contains("href=\"/about\""), "Home lists articles only with root short links");
    Check(home.Contains("https://avatar.exlb.net/" + admin.UserName), "Default site avatar appears");
    var html = parser.ParseDocument(home);
    Check(html.QuerySelector("a[href^='/feed.xml']")?.GetAttribute("target") == "_blank", "RSS opens a new tab");
    Check(html.QuerySelector("a[href='/sitemap.xml']")?.GetAttribute("rel")?.Contains("noopener") == true, "Sitemap opens safely in a new tab");
    var about = await Get(visitor, "/about");
    Check(about.Contains("Introduction") && !about.Contains("class=\"sidebar\"") && !about.Contains("id=\"comments\""), "Page has body without sidebar or comments");
    var welcome = await Get(visitor, "/welcome");
    Check(welcome.Contains("id=\"comments\"") && welcome.Contains("class=\"sidebar\""), "Article retains comments and sidebar");
    Check((await visitor.GetAsync("/p/welcome?cp=2")).Headers.Location?.ToString() == "/welcome/comments/2", "Legacy article URL redirects to comment path");
    Check((await visitor.GetAsync("/p/about")).Headers.Location?.ToString() == "/about", "Legacy page URL redirects");
    Check((await visitor.GetAsync("/missing-short-link")).StatusCode == HttpStatusCode.NotFound, "Unknown short link returns 404");
    Check((await Get(visitor, "/feed.xml")).Contains(baseUrl + "/welcome") && !(await Get(visitor, "/feed.xml")).Contains(baseUrl + "/about"), "RSS includes root article URLs and excludes pages");
    Check((await Get(visitor, "/sitemap.xml")).Contains(baseUrl + "/about"), "Sitemap includes root page URLs");
    var en = await Get(visitor, "/?culture=en");
    Check(en.Contains("lang=\"en\"") && en.Contains("Latest posts") && en.Contains("Read post"), "English theme renders translated labels");
    Check((await Get(visitor, "/Account/Login?culture=en")).Contains("Arithmetic check"), "English account UI includes arithmetic validation");
    Check((await Get(visitor, "/?culture=fr-FR")).Contains("Accueil"), "Custom CultureInfo language renders in theme");
    Check((await visitor.GetAsync("/language?culture=fr-FR&returnUrl=%2Fabout")).Headers.Location?.ToString() == "/about", "Language switch returns to current page");
    Check((await Get(visitor, "/")).Contains("lang=\"fr-FR\""), "Language selection persists in cookie");
    Check((await visitor.GetAsync("/language?culture=fr-FR&returnUrl=https://example.org")).Headers.Location?.ToString() == "/", "Language switch rejects external return URL");
    Check((await visitor.GetAsync("/language?culture=not-a-pack")).StatusCode == HttpStatusCode.BadRequest, "Unknown language rejected");

    using var detected = Client();
    async Task<string> BrowserLanguage(string languages)
    {
        detected.DefaultRequestHeaders.Remove("Accept-Language");
        detected.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", languages);
        return parser.ParseDocument(await Get(detected, "/")).DocumentElement.GetAttribute("lang")!;
    }
    Check(await BrowserLanguage("en-US,en;q=0.9,zh;q=0.8") == "en", "Browser English region selects enabled English");
    Check(await BrowserLanguage("en;q=0.3,fr-FR;q=0.9") == "fr-FR", "Browser language quality controls priority");
    Check(await BrowserLanguage("zh-TW,zh;q=0.9,en;q=0.8") == "zh-Hans", "Unavailable Chinese browser language selects Simplified Chinese");
    Check(await BrowserLanguage("ja-JP") == "en", "Unavailable non-Chinese browser language selects English");
    Check(await BrowserLanguage("en;q=0,fr-FR;q=0.8") == "fr-FR", "Browser languages with zero quality are ignored");
    await detected.GetAsync("/language?culture=zh-Hans&returnUrl=%2F");
    Check(await BrowserLanguage("en-US") == "zh-Hans", "Manual language choice overrides subsequent browser preferences");
    Check((await Get(detected, "/?culture=fr-FR")).Contains("lang=\"fr-FR\""), "Explicit URL culture remains available for language-specific links");
    Check((await Get(detected, "/?culture=disabled")).Contains("lang=\"zh-Hans\""), "Invalid URL language does not discard a manual choice");
    Check(parser.ParseDocument(home).QuerySelector(".language-nav > button") == null, "Language selector has no visible confirmation button");

    using var signedIn = Client();
    var login = await Form(signedIn, "/Account/Login?culture=en", true);
    login["Login"] = admin.UserName; login["Password"] = password;
    var loginResult = await Send(signedIn, "/Account/Login?culture=en", login);
    Check(loginResult.StatusCode == HttpStatusCode.Redirect, "Login accepts correct arithmetic and credentials");
    await CacheChecks.RunAsync(db, signedIn, new Uri(baseUrl), storage, Check);
    await FeatureChecks.RunAsync(db, signedIn, visitor, admin, Check);
    Check((await Get(signedIn, "/Admin/Settings?culture=en")).Contains("Registration mode"), "English admin settings render");
    var mailForm = await Form(signedIn, "/Admin/Settings?culture=en");
    mailForm["Mail.Host"] = "127.0.0.1"; mailForm["Mail.Port"] = smtp.Port.ToString();
    mailForm["Mail.Security"] = "None"; mailForm["Mail.From"] = "admin-configured@example.test";
    mailForm["Mail.Password"] = "http-private-password";
    Check(!parser.ParseDocument(await Get(visitor, "/Account/Register")).QuerySelectorAll("input").Any(e => e.GetAttribute("name") == "Email"), "Registration is unavailable before SMTP configuration");
    Check((await Send(signedIn, "/Admin/Settings?handler=Mail", mailForm)).StatusCode == HttpStatusCode.Redirect, "Administrator saves SMTP settings without editing general settings");
    var mailPage = await Get(signedIn, "/Admin/Settings?culture=en");
    Check(mailPage.Contains("Email settings saved and applied") && mailPage.Contains("A password is saved") && !mailPage.Contains("http-private-password"), "SMTP page confirms save without exposing the password");
    Check(!File.ReadAllText(Path.Combine(storage, "mail.json")).Contains("http-private-password"), "HTTP SMTP settings store an encrypted password");
    Check(parser.ParseDocument(mailPage).QuerySelector("input[name='Mail.Password']")?.GetAttribute("value") is null or "", "SMTP password input is empty on GET");
    var savedMail = File.ReadAllText(Path.Combine(storage, "mail.json"));
    mailForm["Mail.Password"] = "";
    foreach (var (field, value) in new[] { ("Mail.Port", "0"), ("Mail.Port", "not-a-number"), ("Mail.Security", "Auto"), ("Mail.Host", "smtp://host:587"), ("Mail.From", "invalid-address") })
    {
        var invalidMail = new Dictionary<string, string>(mailForm) { [field] = value };
        var response = await Send(signedIn, "/Admin/Settings?handler=Mail", invalidMail);
        Check(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict && File.ReadAllText(Path.Combine(storage, "mail.json")) == savedMail, "Invalid SMTP field leaves saved settings intact: " + field + "=" + value);
    }
    using (var anonymous = Client())
    {
        var anonymousForm = await Form(anonymous, "/Account/Login");
        foreach (var field in mailForm.Where(p => p.Key != "__RequestVerificationToken")) anonymousForm[field.Key] = field.Value;
        Check((await Send(anonymous, "/Admin/Settings?handler=Mail", anonymousForm)).StatusCode == HttpStatusCode.Redirect && File.ReadAllText(Path.Combine(storage, "mail.json")) == savedMail, "Anonymous callers cannot change SMTP settings");
    }
    var noCsrf = new Dictionary<string, string>(mailForm); noCsrf.Remove("__RequestVerificationToken");
    Check((await Send(signedIn, "/Admin/Settings?handler=Mail", noCsrf)).StatusCode == HttpStatusCode.BadRequest, "SMTP settings require an antiforgery token");
    mailForm["ClearMailPassword"] = "true";
    Check((await Send(signedIn, "/Admin/Settings?handler=Mail", mailForm)).StatusCode == HttpStatusCode.Redirect &&
        JsonDocument.Parse(File.ReadAllText(Path.Combine(storage, "mail.json"))).RootElement.GetProperty("Password").GetString() == "", "Administrator can clear SMTP password");
    var editorDocument = parser.ParseDocument(await Get(signedIn, "/Admin/Edit/" + pageId + "?culture=en"));
    var editorLanguages = editorDocument.QuerySelectorAll("#content-language option");
    Check(editorLanguages.Length == 3 && editorLanguages[0].TextContent.Contains("Site default language") && !editorLanguages.Any(o => o.GetAttribute("value") == "zh-Hans"), "Editor has one named default-language option and no duplicate");
    Check(editorDocument.QuerySelector("[data-content-language=''] [name='Draft.Summary']") != null && editorDocument.QuerySelector("[data-content-language=''] [name='Draft.CoverUrl']") != null, "Default summary and cover switch with the edited language");
    foreach (var route in new[] { "/Admin", "/Admin/Users", "/Admin/Navigation", "/Admin/Themes", "/Admin/Media", "/Admin/Comments", "/Admin/Categories", "/Admin/Edit/" + pageId })
        Check((await signedIn.GetAsync(route + "?culture=en")).StatusCode == HttpStatusCode.OK, "Admin page renders: " + route);

    async Task<HttpStatusCode> UploadTheme(string id, string? pageTemplate)
    {
        using var archive = new MemoryStream();
        using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("theme.json").Open()))
                writer.Write(JsonSerializer.Serialize(new { id, name = id, version = 1 }));
            foreach (var template in ThemeService.Templates)
            {
                using var writer = new StreamWriter(zip.CreateEntry(template).Open());
                writer.Write(await File.ReadAllTextAsync(Path.Combine(root, "WordWebCMS/theme/default", template)));
            }
            if (pageTemplate != null)
            {
                using var writer = new StreamWriter(zip.CreateEntry("page.liquid").Open());
                writer.Write(pageTemplate);
            }
        }
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent((await Form(signedIn, "/Admin/Themes"))["__RequestVerificationToken"]), "__RequestVerificationToken");
        form.Add(new ByteArrayContent(archive.ToArray()), "upload", id + ".zip");
        using var response = await signedIn.PostAsync("/Admin/Themes?handler=Upload", form);
        if (id == "custom-page" && response.StatusCode != HttpStatusCode.Redirect)
            Console.WriteLine("Theme upload failed: " + response.StatusCode + " " + await response.Content.ReadAsStringAsync());
        return response.StatusCode;
    }
    Check(await UploadTheme("invalid-page", "{% if %}") == HttpStatusCode.Conflict && !Directory.Exists(Path.Combine(storage, "theme/invalid-page")), "Malformed optional page template is rejected before installation");
    Check(await UploadTheme("milky", null) == HttpStatusCode.Conflict, "Upload cannot replace the bundled milky theme");
    Check(await UploadTheme("large-page", new string('x', 256 * 1024 + 1)) == HttpStatusCode.Conflict, "Optional page template obeys the template size limit");
    Check(await UploadTheme("custom-page", "<h1>Custom page</h1>{{ post.html | raw }}") == HttpStatusCode.Redirect, "Valid optional page template installs");
    var themeForm = await Form(signedIn, "/Admin/Themes"); themeForm["theme"] = "custom-page";
    Check((await Send(signedIn, "/Admin/Themes?handler=Activate", themeForm)).StatusCode == HttpStatusCode.Redirect && (await Get(visitor, "/about")).Contains("Custom page"), "Activation renders the custom page template");
    themeForm["theme"] = "default";
    await Send(signedIn, "/Admin/Themes?handler=Activate", themeForm);
    await File.WriteAllTextAsync(Path.Combine(storage, "theme/custom-page/page.liquid"), "{% if %}");
    themeForm["theme"] = "custom-page";
    Check((await Send(signedIn, "/Admin/Themes?handler=Activate", themeForm)).StatusCode == HttpStatusCode.Conflict && db.Site.Select<SiteSettings>().First().Theme == "default", "Activation rejects a broken page template without switching themes");
    Check(await UploadTheme("fallback-page", null) == HttpStatusCode.Redirect, "Theme may omit the optional page template");
    themeForm["theme"] = "fallback-page";
    Check((await Send(signedIn, "/Admin/Themes?handler=Activate", themeForm)).StatusCode == HttpStatusCode.Redirect && (await Get(visitor, "/about")).Contains("Introduction"), "Missing page template uses the built-in page body");
    themeForm["theme"] = "default";
    await Send(signedIn, "/Admin/Themes?handler=Activate", themeForm);
    Check(Directory.Exists(Path.Combine(storage, "theme/milky")), "Milky is installed as an optional bundled theme");
    themeForm["theme"] = "milky";
    Check((await Send(signedIn, "/Admin/Themes?handler=Activate", themeForm)).StatusCode == HttpStatusCode.Redirect, "Milky templates accept activation with empty data");
    db.Site.Update<Post>().Set(p => p.PinOrder, 1).Where(p => p.Id == articleId).ExecuteAffrows();
    var hiddenId = Publish(PostKind.Article, "Milky private publication", "milky-private");
    db.Site.Update<Post>().Set(p => p.IsPublic, false).Set(p => p.PinOrder, 100).Where(p => p.Id == hiddenId).ExecuteAffrows();
    var hiddenTagId = db.Site.Insert(new Tag { Name = "Milky private tag" }).ExecuteIdentity();
    var hiddenRevisionId = db.Site.Select<Post>().Where(p => p.Id == hiddenId).First().PublishedRevisionId!.Value;
    db.Site.Insert(new RevisionTag { RevisionId = hiddenRevisionId, TagId = hiddenTagId }).ExecuteAffrows();
    db.Site.Insert(new Comment { PostId = hiddenId, UserId = admin.Id, Approved = true, Markdown = "Private comment" }).ExecuteAffrows();
    db.Site.Insert(new Comment { PostId = articleId, UserId = admin.Id, Approved = false, Markdown = "Pending comment" }).ExecuteAffrows();
    var milky = parser.ParseDocument(await Get(visitor, "/"));
    Check(milky.QuerySelector(".featured-note a")?.GetAttribute("href") == "/welcome" && milky.QuerySelector(".pin-badge") != null, "Milky features the real public pinned article");
    Check(!milky.QuerySelectorAll("a").Any(a => a.GetAttribute("href") == "/milky-private"), "Milky recent and featured widgets exclude withdrawn articles");
    var stats = milky.QuerySelectorAll(".site-stats dd");
    Check(stats.Length == 4 && stats[0].TextContent == db.Site.Select<Post>().Where(p => p.IsPublic && p.Kind == PostKind.Article && p.PublishedRevisionId != null).Count().ToString()
        && stats[1].TextContent == "0" && stats[2].TextContent == "0", "Milky statistics exclude pages, private tags, withdrawn and pending comments");
    Check(parser.ParseDocument(await Get(visitor, "/search/Welcome")).QuerySelector(".featured-note") == null, "Search does not show the home pinned banner");
    var milkyPost = parser.ParseDocument(await Get(signedIn, "/welcome"));
    Check(milkyPost.QuerySelector(".article-neighbors a") != null && milkyPost.QuerySelector(".related-grid a") != null, "Milky article provides adjacent and related public articles");
    Check(!milkyPost.QuerySelectorAll(".article-neighbors a, .related-grid a").Any(a => a.GetAttribute("href") is "/milky-private" or "/about" or "/welcome"), "Milky article links exclude private content, pages and itself");
    Check(milkyPost.QuerySelectorAll("form[method=post]").All(f => f.QuerySelector("input[name=__RequestVerificationToken]") != null), "Milky write actions carry CSRF tokens");
    var milkyPage = parser.ParseDocument(await Get(visitor, "/about"));
    Check(milkyPage.QuerySelector(".single-column .standalone-page") != null && milkyPage.QuerySelector(".sidebar, #comments, .article-actions") == null, "Milky standalone page has no article widgets");
    Check((await visitor.GetAsync("/theme-assets/milky/assets/site.css")).StatusCode == HttpStatusCode.OK &&
        (await visitor.GetAsync("/theme-assets/milky/assets/appearance.js")).StatusCode == HttpStatusCode.OK, "Milky bundled assets are served");
    var milkyError = await visitor.GetAsync("/missing-milky-route");
    Check(milkyError.StatusCode == HttpStatusCode.NotFound && parser.ParseDocument(await milkyError.Content.ReadAsStringAsync()).QuerySelector(".error-code")?.TextContent == "404", "Milky renders themed 404 without changing HTTP status");
    Check(WebUtility.HtmlDecode(await Get(visitor, "/?culture=en")).Contains("Site statistics"), "Milky new UI labels are translated");
    var oldRegistration = db.Site.Select<SiteSettings>().First().Registration;
    db.Site.Update<SiteSettings>().Set(s => s.Registration, RegistrationMode.Closed).Where(s => s.Id == 1).ExecuteAffrows();
    Check(!parser.ParseDocument(await Get(visitor, "/welcome")).QuerySelectorAll("a").Any(a => a.GetAttribute("href") == "/Account/Register"), "Milky respects closed registration in article and footer");
    db.Site.Update<SiteSettings>().Set(s => s.Registration, oldRegistration).Where(s => s.Id == 1).ExecuteAffrows();
    db.Site.Delete<Comment>().Where(c => c.PostId == hiddenId || c.PostId == articleId && !c.Approved).ExecuteAffrows();
    db.Site.Delete<RevisionTag>().Where(t => t.RevisionId == hiddenRevisionId).ExecuteAffrows();
    db.Site.Delete<Tag>().Where(t => t.Id == hiddenTagId).ExecuteAffrows();
    db.Site.Delete<PostDraft>().Where(p => p.PostId == hiddenId).ExecuteAffrows();
    db.Site.Delete<PostRevision>().Where(p => p.PostId == hiddenId).ExecuteAffrows();
    db.Site.Delete<Post>().Where(p => p.Id == hiddenId).ExecuteAffrows();
    db.Site.Update<Post>().Set(p => p.PinOrder, 0).Where(p => p.Id == articleId).ExecuteAffrows();
    themeForm["theme"] = "default";
    await Send(signedIn, "/Admin/Themes?handler=Activate", themeForm);
    login["__RequestVerificationToken"] = (await Form(signedIn, "/Account/Login"))["__RequestVerificationToken"];
    var replay = await Send(signedIn, "/Account/Login?culture=en", login);
    Check(WebUtility.HtmlDecode(await replay.Content.ReadAsStringAsync()).Contains("arithmetic answer is incorrect"), "HTTP login rejects replayed arithmetic");
    var comment = await Form(signedIn, "/about"); comment["body"] = "Not allowed";
    Check((await Send(signedIn, "/about?handler=Comment", comment)).StatusCode == HttpStatusCode.Conflict, "Direct POST cannot comment on page");
    Check(!db.Site.Select<Comment>().Where(c => c.PostId == pageId).Any(), "No page comment was stored");
    Check((await Send(signedIn, "/about?handler=Like", comment)).StatusCode == HttpStatusCode.Conflict, "Direct POST cannot like page");

    db.Site.Update<SiteSettings>().Set(s => s.Registration, RegistrationMode.Closed).Where(s => s.Id == 1).ExecuteAffrows();
    Check((await Get(visitor, "/Account/Register?culture=en")).Contains("Registration is closed"), "Closed registration hides signup form");
    var registration = await Form(visitor, "/Account/Login", true);
    registration["UserName"] = "closed-user"; registration["Email"] = "closed@example.test"; registration["Password"] = password;
    await Send(visitor, "/Account/Register", registration);
    Check(!db.Accounts.Select<CmsUser>().Where(u => u.UserName == "closed-user").Any(), "Closed registration also blocks direct POST");
    var shared = await accounts.CreateAsync("shared-user", "shared@example.test", password, true);
    var sharedLogin = await Form(visitor, "/Account/Login?culture=en", true);
    sharedLogin["Login"] = shared.UserName; sharedLogin["Password"] = password;
    Check((await Send(visitor, "/Account/Login?culture=en", sharedLogin)).StatusCode == HttpStatusCode.Forbidden, "Closed site rejects new shared-account membership");

    async Task<CmsUser> Register(string name, RegistrationMode mode)
    {
        db.Site.Update<SiteSettings>().Set(s => s.Registration, mode).Where(s => s.Id == 1).ExecuteAffrows();
        var form = await Form(visitor, "/Account/Register?culture=en", true);
        form["UserName"] = name; form["Email"] = name + "@example.test"; form["Password"] = password;
        var response = await Send(visitor, "/Account/Register?culture=en", form);
        Check(WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()).Contains("Verification email sent"), "Registration sends verification email: " + mode);
        var account = db.Accounts.Select<CmsUser>().Where(u => u.UserName == name).First();
        Check(account != null && !account.EmailConfirmed, "Registration requires email confirmation: " + mode);
        using var messageStream = new MemoryStream(Encoding.UTF8.GetBytes(smtp.Messages.Last()));
        var message = MimeKit.MimeMessage.Load(messageStream);
        Check(message.From.Mailboxes.Single().Address == "admin-configured@example.test", "Verification uses admin SMTP settings immediately");
        var token = Regex.Match(message.TextBody!, @"token=([A-F0-9]{64})").Groups[1].Value;
        Check(token.Length == 64, "Verification email has a usable token");
        accounts.ConsumeToken(token, "verify", null);
        return account!;
    }
    var open = await Register("open-user", RegistrationMode.Open);
    Check(db.Site.Select<SiteUserRole>().Where(r => r.UserId == open.Id).First().Role == SiteRole.Reader, "Open registration grants Reader membership");
    var pending = await Register("pending-user", RegistrationMode.Approval);
    Check(db.Site.Select<SiteUserRole>().Where(r => r.UserId == pending.Id).First().Role == SiteRole.Pending, "Approval registration grants no active role");
    var pendingLogin = await Form(visitor, "/Account/Login?culture=en", true);
    pendingLogin["Login"] = pending.UserName; pendingLogin["Password"] = password;
    var denied = await Send(visitor, "/Account/Login?culture=en", pendingLogin);
    Check(denied.StatusCode == HttpStatusCode.Forbidden && (await denied.Content.ReadAsStringAsync()).Contains("awaiting approval"), "Verified but pending user cannot log in");
    Check((await Get(signedIn, "/Admin/Users?culture=en")).Contains(pending.UserName), "Admin sees pending registrations");
    var approval = await Form(signedIn, "/Admin/Users"); approval["login"] = pending.UserName; approval["role"] = "Reader";
    Check((await Send(signedIn, "/Admin/Users", approval)).StatusCode == HttpStatusCode.Redirect, "Administrator approves a registration");
    db.Site.Insert(new SiteUserRole { UserId = shared.Id, Role = SiteRole.Editor }).ExecuteAffrows();
    var invalidRole = await Form(signedIn, "/Admin/Users"); invalidRole["login"] = shared.UserName; invalidRole["role"] = "not-a-role";
    Check((await Send(signedIn, "/Admin/Users", invalidRole)).StatusCode == HttpStatusCode.BadRequest &&
        db.Site.Select<SiteUserRole>().Where(r => r.UserId == shared.Id).First().Role == SiteRole.Editor, "Invalid role binding cannot overwrite site membership");
    pendingLogin = await Form(visitor, "/Account/Login?culture=en", true);
    pendingLogin["Login"] = pending.UserName; pendingLogin["Password"] = password;
    Check((await Send(visitor, "/Account/Login?culture=en", pendingLogin)).StatusCode == HttpStatusCode.Redirect, "Approved and verified member can log in");
    var readerMail = await Form(visitor, "/Account/Login");
    foreach (var field in mailForm.Where(p => p.Key != "__RequestVerificationToken")) readerMail[field.Key] = field.Value;
    var beforeReaderMail = File.ReadAllText(Path.Combine(storage, "mail.json"));
    var readerMailResponse = await Send(visitor, "/Admin/Settings?handler=Mail", readerMail);
    Check(readerMailResponse.StatusCode == HttpStatusCode.Redirect && readerMailResponse.Headers.Location?.ToString().Contains("/Error/403") == true &&
        File.ReadAllText(Path.Combine(storage, "mail.json")) == beforeReaderMail, "Readers cannot change administrator SMTP settings");
    db.Site.Update<SiteUserRole>().Set(r => r.Role, SiteRole.Banned).Where(r => r.UserId == pending.Id).ExecuteAffrows();
    var bannedPage = await Get(visitor, "/welcome?culture=en");
    Check(!bannedPage.Contains("action=\"/Account/Logout\""), "Banning invalidates existing member cookie");

    var recovery = await Form(visitor, "/Account/Recover", true); recovery["Email"] = open.Email;
    Check((await Send(visitor, "/Account/Recover", recovery)).StatusCode == HttpStatusCode.OK, "Account recovery accepts valid arithmetic");
    using var resetMessageStream = new MemoryStream(Encoding.UTF8.GetBytes(smtp.Messages.Last()));
    var resetMessage = MimeKit.MimeMessage.Load(resetMessageStream);
    var resetToken = Regex.Match(resetMessage.TextBody!, @"token=([A-F0-9]{64})").Groups[1].Value;
    var reset = await Form(visitor, "/Account/Reset?token=" + resetToken, true); reset["Password"] = password + "-reset";
    Check((await Send(visitor, "/Account/Reset", reset)).StatusCode == HttpStatusCode.OK && await accounts.CheckPasswordAsync(open.UserName, password + "-reset") != null, "Password reset validates arithmetic and changes password");

    var siteForm = await Form(signedIn, "/Admin/Settings");
    siteForm["Settings.Title"] = "Updated regression"; siteForm["Settings.BaseUrl"] = baseUrl;
    siteForm["Settings.AvatarUserName"] = "avatar-user"; siteForm["Settings.AvatarUrl"] = "/media/site.png";
    siteForm["SelectedLanguages[0]"] = "zh-CN"; siteForm["SelectedLanguages[1]"] = "en"; siteForm["SelectedLanguages[2]"] = "fr-FR";
    siteForm["Translations[en].Title"] = "Translated site"; siteForm["Translations[en].Description"] = "Translated description";
    siteForm["Settings.Language"] = "fr-FR"; siteForm["Settings.Registration"] = "Closed";
    Check((await Send(signedIn, "/Admin/Settings", siteForm)).StatusCode == HttpStatusCode.Redirect, "Admin persists avatar, custom language and registration mode");
    siteForm["Settings.Registration"] = "not-a-mode";
    Check((await Send(signedIn, "/Admin/Settings", siteForm)).StatusCode == HttpStatusCode.BadRequest &&
        db.Site.Select<SiteSettings>().First().Registration == RegistrationMode.Closed, "Invalid registration binding cannot reopen registration");
    siteForm["Settings.Registration"] = "Closed";
    using var fresh = Client();
    var customized = await Get(fresh, "/");
    Check(customized.Contains("lang=\"fr-FR\"") && customized.Contains("/media/site.png") && !customized.Contains("href=\"/Account/Register\""), "Site default culture, custom avatar and closed registration apply");
    siteForm["__RequestVerificationToken"] = (await Form(signedIn, "/Admin/Settings"))["__RequestVerificationToken"];
    siteForm["Settings.AvatarUrl"] = "javascript:alert(1)";
    Check((await Send(signedIn, "/Admin/Settings", siteForm)).StatusCode == HttpStatusCode.Conflict, "Unsafe avatar URL is rejected");
    Check(customized.Contains("Updated regression"), "Site default language uses base site title");
    var translatedSite = await Get(fresh, "/?culture=en");
    Check(translatedSite.Contains("Translated site") && translatedSite.Contains("Translated description"), "Site title and description use content translations");
    var original = db.Site.Select<Post>().Where(p => p.Id == articleId).First().PublishedRevisionId!.Value;
    var translatedDraft = db.Site.Select<PostDraft>().Where(d => d.PostId == articleId).First();
    translatedDraft.Translations = ContentLanguages.Write(new Dictionary<string, ContentTranslation> {
        ["en"] = new() { Title = "English welcome translation", Summary = "English summary", Markdown = "## Translated body" }
    });
    content.Save(articleId, translatedDraft, Principal(admin.Id), "autosave");
    Check(!(await Get(fresh, "/welcome?culture=en")).Contains("English welcome translation"), "Translation autosave does not affect public revision");
    translatedDraft = db.Site.Select<PostDraft>().Where(d => d.PostId == articleId).First();
    content.Save(articleId, translatedDraft, Principal(admin.Id), "publish");
    var translatedHtml = await Get(fresh, "/welcome?culture=en");
    Check(translatedHtml.Contains("English welcome translation") && translatedHtml.Contains("Translated body"), "Published article title and body are localized");
    Check((await Get(fresh, "/?culture=en")).Contains("English welcome translation"), "Article lists use translated titles");
    Check((await Get(fresh, "/feed.xml?culture=en")).Contains("English welcome translation"), "RSS uses translated titles");
    Check((await Get(fresh, "/welcome?culture=fr-FR")).Contains("Introduction"), "Site default language renders base article content");
    db.Site.Update<SiteSettings>().Set(s => s.Language, "zh-Hans").Where(s => s.Id == 1).ExecuteAffrows();
    Check((await Get(fresh, "/welcome?culture=fr-FR")).Contains("Translated body"), "Missing non-Chinese article translation renders English through HTTP");
    Check((await Get(fresh, "/?culture=fr-FR")).Contains("English welcome translation") && (await Get(fresh, "/feed.xml?culture=fr-FR")).Contains("English welcome translation"), "Lists and RSS share the English translation fallback");
    db.Site.Update<SiteSettings>().Set(s => s.Language, "fr-FR").Where(s => s.Id == 1).ExecuteAffrows();
    content.Restore(articleId, original, translatedDraft.Version, Principal(admin.Id));
    Check(ContentLanguages.Read<ContentTranslation>(db.Site.Select<PostDraft>().Where(d => d.PostId == articleId).First().Translations).Count == 0, "Revision restore also restores translations");
    var pageEdit = await Form(signedIn, "/Admin/Edit/" + pageId);
    pageEdit["Draft.Title"] = "About"; pageEdit["Draft.Slug"] = "about"; pageEdit["Draft.Markdown"] = "Original page";
    pageEdit["Translations[en].Title"] = "About in English"; pageEdit["Translations[en].Markdown"] = "Localized page body"; pageEdit["action"] = "publish";
    Check((await Send(signedIn, "/Admin/Edit/" + pageId, pageEdit)).StatusCode == HttpStatusCode.Redirect, "Editor binds and publishes a language translation");
    var localizedPage = await Get(fresh, "/about?culture=en");
    Check(localizedPage.Contains("About in English") && localizedPage.Contains("Localized page body") && !localizedPage.Contains("id=\"comments\""), "Page translations render without comments");
    db.Site.Update<SiteSettings>().Set(s => s.EnabledLanguages, "fr-FR").Where(s => s.Id == 1).ExecuteAffrows();
    var disabledHtml = await Get(fresh, "/welcome?culture=en");
    Check(disabledHtml.Contains("lang=\"fr-FR\"") && !disabledHtml.Contains("English welcome translation"), "Disabled requested language falls back to configured default");
    Check((await fresh.GetAsync("/language?culture=en")).StatusCode == HttpStatusCode.BadRequest, "Language switch rejects installed but disabled language");
    Check((await fresh.GetAsync("/language?culture=fr-FR&returnUrl=%2Fwelcome%3Fculture%3Den%26cp%3D2")).Headers.Location?.ToString() == "/welcome?cp=2", "Language switch removes stale culture query but preserves other filters");
    var choices = parser.ParseDocument(disabledHtml).QuerySelectorAll("select[name=culture] option");
    Check(choices.Length == 1 && choices[0].GetAttribute("value") == "fr-FR", "Language dropdown lists only administrator-enabled languages");
    await File.WriteAllLinesAsync(Path.Combine(storage, "web.log"), logs);

}
finally
{
    await File.WriteAllLinesAsync(Path.Combine(storage, "web.log"), logs);
    if (web != null && !web.HasExited) { web.Kill(entireProcessTree: true); await web.WaitForExitAsync(); }
    db.Dispose();
    // Only the freshly generated, isolated database is removed.
    await new MySqlCommand($"DROP DATABASE `{database}`", server).ExecuteNonQueryAsync();
}
Console.WriteLine($"All {passed} checks passed.");

sealed class TestSmtp : IDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    public ConcurrentQueue<string> Messages { get; } = new();
    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
    public TestSmtp() { listener.Start(); _ = Serve(); }
    private async Task Serve()
    {
        try
        {
            while (true)
            {
                using var socket = await listener.AcceptTcpClientAsync();
                using var stream = socket.GetStream();
                using var reader = new StreamReader(stream);
                using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
                await writer.WriteLineAsync("220 regression SMTP");
                while (await reader.ReadLineAsync() is { } command)
                {
                    if (command.StartsWith("DATA"))
                    {
                        await writer.WriteLineAsync("354 Send message"); var body = new StringBuilder();
                        while (await reader.ReadLineAsync() is { } line && line != ".") body.AppendLine(line);
                        Messages.Enqueue(body.ToString()); await writer.WriteLineAsync("250 Accepted");
                    }
                    else if (command.StartsWith("QUIT")) { await writer.WriteLineAsync("221 Bye"); break; }
                    else await writer.WriteLineAsync("250 OK");
                }
            }
        }
        catch (SocketException) { }
        catch (ObjectDisposedException) { }
    }
    public void Dispose() => listener.Stop();
}
