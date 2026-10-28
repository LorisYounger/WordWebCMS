using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using WordWebCMS.Data;
using WordWebCMS.Models;
using WordWebCMS.Pages;
using WordWebCMS.Services;

internal static class SetupChecks
{
    public static async Task RunAsync(string root, string connectionString, Action<bool, string> check)
    {
        var prefix = "wwcms_setup_" + Guid.NewGuid().ToString("N");
        var databases = new[] { prefix + "_web", prefix + "_site", prefix + "_accounts", prefix + "_legacy", prefix + "_auto" };
        var connection = new MySqlConnectionStringBuilder(connectionString) { Database = "" };
        using var server = new MySqlConnection(connection.ConnectionString);
        await server.OpenAsync();
        var storage = Path.Combine(root, "artifacts", prefix);
        var baseUrl = "http://127.0.0.1:5194";
        Process? web = null;
        var logs = new ConcurrentQueue<string>();
        var parser = new HtmlParser();
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.Combine(root, "WordWebCMS"),
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "WordWebCMS.dll"));
        start.ArgumentList.Add("--urls"); start.ArgumentList.Add(baseUrl);
        start.Environment["ConnectionStrings__Accounts"] = "";
        start.Environment["ConnectionStrings__Site"] = "";
        start.Environment["Setup__Token"] = "";
        start.Environment["Storage__Path"] = storage;
        start.Environment["Site__CookieName"] = prefix;
        start.Environment["ASPNETCORE_ENVIRONMENT"] = OperatingSystem.IsWindows() ? "Development" : "Production";
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() };
        using var client = new HttpClient(handler) { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(30) };
        async Task StartAsync()
        {
            logs.Clear();
            web = Process.Start(start)!;
            web.OutputDataReceived += (_, e) => { if (e.Data != null) logs.Enqueue(e.Data); };
            web.ErrorDataReceived += (_, e) => { if (e.Data != null) logs.Enqueue(e.Data); };
            web.BeginOutputReadLine(); web.BeginErrorReadLine();
            for (var attempt = 0; ; attempt++)
            {
                try { await client.GetAsync("/Setup"); break; }
                catch (HttpRequestException) when (attempt < 100 && !web.HasExited) { await Task.Delay(200); }
            }
            check(!web.HasExited, "Setup host starts successfully");
        }
        async Task StopAsync()
        {
            if (web != null && !web.HasExited) { web.Kill(entireProcessTree: true); await web.WaitForExitAsync(); }
            web?.Dispose(); web = null;
        }
        async Task<Dictionary<string, string>> FormAsync(string? token = null)
        {
            var page = await client.GetAsync("/Setup");
            var html = page.StatusCode == HttpStatusCode.Redirect ? await client.GetStringAsync("/Admin") : await page.Content.ReadAsStringAsync();
            var document = parser.ParseDocument(html);
            var data = new Dictionary<string, string> { ["__RequestVerificationToken"] = document.QuerySelector("input[name=__RequestVerificationToken]")!.GetAttribute("value")! };
            if (token != null) data["Token"] = token;
            return data;
        }
        async Task<HttpResponseMessage> SendAsync(string action, Dictionary<string, string> data) =>
            await client.PostAsync("/Setup" + action, new FormUrlEncodedContent(data));
        string ReadToken()
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(storage, "setup.json")));
            return document.RootElement.GetProperty("Setup").GetProperty("Token").GetString()!;
        }
        void FillDatabase(Dictionary<string, string> data, string name, string field = "SiteDatabase")
        {
            data[field + ".Server"] = connection.Server;
            data[field + ".Port"] = connection.Port.ToString();
            data[field + ".Name"] = name;
            data[field + ".UserName"] = connection.UserID;
            data[field + ".Password"] = connection.Password;
            data[field + ".SslMode"] = connection.SslMode.ToString();
        }
        CmsDb Database(string site, string? accounts = null)
        {
            var siteConnection = new MySqlConnectionStringBuilder(connection.ConnectionString) { Database = site };
            var accountsConnection = new MySqlConnectionStringBuilder(connection.ConnectionString) { Database = accounts ?? site };
            return new CmsDb(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:Site"] = siteConnection.ConnectionString, ["ConnectionStrings:Accounts"] = accountsConnection.ConnectionString }).Build());
        }
        try
        {
            await StartAsync();
            var token = ReadToken();
            check(token.Length == 64 && logs.Any(line => line.Contains("WordWebCMS Setup Token: " + token)), "Generated setup token is printed in the console");
            var html = await client.GetStringAsync("/Setup");
            check(!html.Contains(token) && !html.Contains("AccountsConnection") && !html.Contains("--initialize"), "Initial page asks for the console token without exposing credentials or requiring commands");
            check((await SendAsync("?handler=Authorize", new() { ["Token"] = token })).StatusCode == HttpStatusCode.BadRequest, "Setup authorization requires antiforgery protection");
            var data = await FormAsync("wrong-token");
            check((await SendAsync("?handler=Initialize", data)).StatusCode == HttpStatusCode.Forbidden, "Invalid token cannot initialize databases");
            data = await FormAsync(token);
            var authorized = await SendAsync("?handler=Authorize", data);
            var cookie = authorized.Headers.GetValues("Set-Cookie").First(value => value.StartsWith(prefix + ".Setup="));
            check(authorized.StatusCode == HttpStatusCode.OK && cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase) &&
                cookie.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase) && cookie.Contains("max-age=604800") && !cookie.Contains(token), "Browser remembers authorization for seven days in a protected HttpOnly cookie");
            html = await client.GetStringAsync("/Setup");
            var document = parser.ParseDocument(html);
            check(document.QuerySelector("input[name=Token]") == null && document.QuerySelector("input[name='SiteDatabase.Server']") != null &&
                document.QuerySelector("input[name='SiteDatabase.Password']")?.GetAttribute("type") == "password", "Refresh retains authorization and shows friendly database fields");
            check(document.QuerySelector("form.setup-form")?.GetAttribute("action") == "/Setup" &&
                document.QuerySelector("form.setup-form button")?.GetAttribute("formaction")?.Contains("handler=Configure") == true,
                "The main form completes setup at its default handler and Enter does not forget authorization");
            check((await client.GetStringAsync("/Setup?culture=en")).Contains("Database address"), "Friendly setup fields are translated into English");
            check(parser.ParseDocument(await client.GetStringAsync("/setup")).QuerySelector("input[name=Token]") == null, "Remembered authorization works with lowercase setup URLs");
            data = await FormAsync();
            FillDatabase(data, databases[0]); data["SiteDatabase.Port"] = "not-a-port";
            check((await SendAsync("?handler=Configure", data)).StatusCode == HttpStatusCode.OK && !File.ReadAllText(Path.Combine(storage, "setup.json")).Contains("ConnectionStrings"), "Invalid port cannot save a configuration");
            data = await FormAsync(); FillDatabase(data, databases[0]); data["SiteDatabase.SslMode"] = "999";
            await SendAsync("?handler=Configure", data);
            check(!File.ReadAllText(Path.Combine(storage, "setup.json")).Contains("ConnectionStrings"), "Invalid encryption mode is rejected");
            data = await FormAsync(); FillDatabase(data, databases[0]);
            var saved = await SendAsync("?handler=Configure", data);
            var savedHtml = parser.ParseDocument(await saved.Content.ReadAsStringAsync());
            using (var database = Database(databases[0]))
                check(saved.StatusCode == HttpStatusCode.OK && database.CheckSchema(), "Save automatically creates the named database and initializes its tables");
            check(savedHtml.QuerySelector("input[name='SiteDatabase.Password']")?.GetAttribute("value") is null or "" &&
                !savedHtml.QuerySelector("fieldset#setup-administrator")!.HasAttribute("disabled"), "Password is not reflected and administrator fields are immediately available");
            data = await FormAsync(); FillDatabase(data, databases[0]); data["SiteDatabase.Password"] = "";
            await SendAsync("?handler=Configure", data);
            using (var savedConfig = JsonDocument.Parse(File.ReadAllText(Path.Combine(storage, "setup.json"))))
            {
                var parsed = new MySqlConnectionStringBuilder(savedConfig.RootElement.GetProperty("ConnectionStrings").GetProperty("Site").GetString()!);
                check(parsed.Password == connection.Password, "Leaving the database password blank preserves its saved value");
                var edited = SetupModel.DatabaseInput.FromConnectionString(parsed.ConnectionString);
                edited.UserName += "_other";
                check(new MySqlConnectionStringBuilder(edited.ToConnectionString(parsed.ConnectionString)).Password == "", "Changing the server account does not reuse the old password");
            }
            check(ReadToken() == token, "Saving database settings preserves the setup token");
            await StopAsync();
            await StartAsync();
            document = parser.ParseDocument(await client.GetStringAsync("/Setup"));
            check(document.QuerySelector("input[name=Token]") == null && document.QuerySelector("input[name='SiteDatabase.Name']")?.GetAttribute("value") == databases[0], "Authorization and database fields survive application restart");
            check(logs.Any(line => line.Contains("WordWebCMS Setup Token: " + token)), "Existing token is printed again after restart");

            var savedConfiguration = File.ReadAllText(Path.Combine(storage, "setup.json"));
            data = await FormAsync(); FillDatabase(data, databases[0]); data["SiteDatabase.Password"] = "wrong-password-diagnostic-marker";
            var rejectedPassword = WebUtility.HtmlDecode(await (await SendAsync("?handler=Configure", data)).Content.ReadAsStringAsync());
            check(rejectedPassword.Contains("1045") && !rejectedPassword.Contains("wrong-password-diagnostic-marker") &&
                File.ReadAllText(Path.Combine(storage, "setup.json")) == savedConfiguration,
                "Connection failures show the MySQL error code without echoing passwords or changing saved configuration");

            var blockedWrite = Path.Combine(storage, "setup.json.tmp");
            Directory.CreateDirectory(blockedWrite);
            try
            {
                data = await FormAsync(); FillDatabase(data, databases[0]);
                var deniedWrite = WebUtility.HtmlDecode(await (await SendAsync("?handler=Configure", data)).Content.ReadAsStringAsync());
                check(deniedWrite.Contains("Docker") && deniedWrite.Contains("UnauthorizedAccessException") &&
                    File.ReadAllText(Path.Combine(storage, "setup.json")) == savedConfiguration,
                    "Configuration write failures identify the container mount permissions and preserve the saved file");
            }
            finally { Directory.Delete(blockedWrite); }

            using (var versionDb = Database(databases[0]))
            {
                versionDb.Site.Update<SchemaVersion>().Set(v => v.Version, 7).Where(v => v.Id == 1).ExecuteAffrows();
                await StopAsync();
                await StartAsync();
                var newerVersion = WebUtility.HtmlDecode(await client.GetStringAsync("/Setup?culture=zh-Hans"));
                check(newerVersion.Contains("版本为 7") && newerVersion.Contains("检查账号数据库版本"),
                    "Startup version failures retain their exact cause and step after refreshing setup");
                data = await FormAsync();
                var failedUpgrade = WebUtility.HtmlDecode(await (await SendAsync("?handler=Initialize", data)).Content.ReadAsStringAsync());
                check(failedUpgrade.Contains("InvalidOperationException") && failedUpgrade.Contains("7") &&
                    versionDb.Site.Select<SchemaVersion>().First().Version == 7,
                    "Browser upgrade exposes the application refusal without downgrading a newer database");
                check(logs.Any(line => line.Contains("Setup failed at") && line.Contains("7")),
                    "Container logs retain a readable migration failure reason");
                versionDb.Site.Update<SchemaVersion>().Set(v => v.Version, 6).Where(v => v.Id == 1).ExecuteAffrows();
            }

            var diagnosticsConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                ["Setup:Token"] = token, ["ConnectionStrings:Site"] = connection.ConnectionString }).Build();
            using (var diagnosticsDb = new CmsDb(diagnosticsConfig))
            {
                var diagnostics = new SetupService(diagnosticsConfig, diagnosticsDb, new StoragePaths(storage),
                    DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(storage, "keys"))), NullLogger<SetupService>.Instance);
                var explanation = diagnostics.ReportFailure(new InvalidOperationException($"failure {connection.Password} {token} submitted-secret password=other-secret"), "submitted-secret");
                check(!explanation.Contains(connection.Password) && !explanation.Contains(token) &&
                    !explanation.Contains("submitted-secret") && !explanation.Contains("other-secret"),
                    "Detailed errors redact configured passwords, setup tokens and submitted secrets");
            }

            data = await FormAsync(); FillDatabase(data, databases[1]);
            FillDatabase(data, databases[2], "AccountsDatabase"); data["UseSeparateAccounts"] = "true";
            await SendAsync("?handler=Configure", data);
            using var finalDb = Database(databases[1], databases[2]);
            check(finalDb.CheckSchema() && !finalDb.Site.DbFirst.ExistsTable("cms_user") && finalDb.Accounts.DbFirst.ExistsTable("cms_user"), "Separate account and site databases initialize automatically");
            var password = "Setup-" + Guid.NewGuid().ToString("N");
            var shared = await new AccountService(finalDb, new PasswordHasher<CmsUser>()).CreateAsync("shared-admin", "setup@example.test", password, true);
            data = await FormAsync(); data["Title"] = "Setup regression"; data["BaseUrl"] = baseUrl;
            data["UserName"] = shared.UserName; data["Email"] = shared.Email; data["Password"] = "incorrect-shared-password";
            check((await SendAsync("", data)).StatusCode == HttpStatusCode.OK && !finalDb.Site.Select<SiteSettings>().Any(), "Existing shared account still requires its original verified password");
            data["Password"] = password;
            var completed = await SendAsync("", data);
            check(completed.StatusCode == HttpStatusCode.Redirect && completed.Headers.Location?.ToString() == "/Admin", "Complete setup uses remembered authorization and signs in the administrator");
            check(finalDb.Accounts.Select<CmsUser>().Count() == 1 && finalDb.Site.Select<SiteUserRole>().Where(r => r.UserId == shared.Id && r.Role == SiteRole.Administrator).Any(), "Setup reuses the shared account and grants only the local administrator role");
            check(finalDb.Site.Select<Post>().Count() == 1 && (await client.GetAsync("/")).StatusCode == HttpStatusCode.OK, "New site is accessible immediately with one welcome article");
            data = await FormAsync(token);
            foreach (var action in new[] { "", "?handler=Configure" })
                check((await SendAsync(action, data)).StatusCode == HttpStatusCode.Conflict, "Completed installation cannot recreate administrators or change connections: " + action);

            // Simulate an upgrade that needs to be resumed while the process remains running.
            finalDb.Accounts.Update<SchemaVersion>().Set(v => v.Version, 3).Where(v => v.Id == 1).ExecuteAffrows();
            finalDb.Site.Update<SchemaVersion>().Set(v => v.Version, 3).Where(v => v.Id == 1).ExecuteAffrows();
            data = await FormAsync(token); // Refresh marks schema unavailable before testing a missing column.
            finalDb.Accounts.Ado.ExecuteNonQuery("ALTER TABLE cms_user DROP COLUMN Bio");
            await SendAsync("?handler=Authorize", data);
            html = WebUtility.HtmlDecode(await client.GetStringAsync("/Setup"));
            check(parser.ParseDocument(html).QuerySelector("button[formaction*='handler=Initialize']") != null && !html.Contains("--initialize") && !html.Contains("入口已锁定"), "Existing site gets a browser upgrade action instead of a command-line lock");
            using (var anonymous = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(baseUrl) })
            {
                var anonymousHtml = parser.ParseDocument(await anonymous.GetStringAsync("/Setup"));
                var untrusted = new Dictionary<string, string> { ["__RequestVerificationToken"] = anonymousHtml.QuerySelector("input[name=__RequestVerificationToken]")!.GetAttribute("value")! };
                check((await anonymous.PostAsync("/Setup?handler=Initialize", new FormUrlEncodedContent(untrusted))).StatusCode == HttpStatusCode.Forbidden, "Anonymous callers cannot upgrade the existing database");
            }
            check((await client.GetAsync("/")).StatusCode == HttpStatusCode.Redirect, "Normal traffic waits at setup while schema needs an upgrade");
            data = await FormAsync();
            var upgraded = await SendAsync("?handler=Initialize", data);
            check(upgraded.StatusCode == HttpStatusCode.Redirect && finalDb.CheckSchema() && finalDb.Accounts.DbFirst.GetTableByName("cms_user").Columns.Any(c => c.Name == "Bio"), "Browser upgrade repairs an installed site without a process restart");
            check(finalDb.Site.Select<Post>().Count() == 1 && finalDb.Accounts.Select<CmsUser>().First().Email == shared.Email, "Browser upgrade preserves existing content and shared accounts");
            await StopAsync();
            finalDb.Accounts.Ado.ExecuteNonQuery("ALTER TABLE cms_user DROP COLUMN Bio");
            finalDb.Accounts.Update<SchemaVersion>().Set(v => v.Version, 3).Where(v => v.Id == 1).ExecuteAffrows();
            finalDb.Site.Update<SchemaVersion>().Set(v => v.Version, 3).Where(v => v.Id == 1).ExecuteAffrows();
            await StartAsync();
            check(finalDb.CheckSchema() && finalDb.Accounts.DbFirst.GetTableByName("cms_user").Columns.Any(c => c.Name == "Bio") &&
                (await client.GetAsync("/")).StatusCode == HttpStatusCode.OK, "Container-style restart automatically upgrades the existing schema before serving traffic");
            check((await client.GetAsync("/Setup")).StatusCode == HttpStatusCode.Redirect && finalDb.Site.Select<Post>().Count() == 1, "Successful automatic upgrade closes setup without reinstalling the site");

            // The remote failure: an installed site with an account database missing only the version table.
            var originalPasswordHash = finalDb.Accounts.Select<CmsUser>().Where(u => u.Id == shared.Id).First().PasswordHash;
            await StopAsync();
            finalDb.Accounts.Ado.ExecuteNonQuery("DROP TABLE schema_version");
            await StartAsync();
            var confirmationPage = parser.ParseDocument(await client.GetStringAsync("/Setup"));
            check(!finalDb.Accounts.DbFirst.ExistsTable("schema_version") &&
                !string.IsNullOrWhiteSpace(confirmationPage.QuerySelector("form.setup-form")?.GetAttribute("data-confirm")),
                "Missing account version table asks for confirmation before changing an installed site");
            data = await FormAsync();
            await SendAsync("?handler=Initialize", data);
            check(!finalDb.Accounts.DbFirst.ExistsTable("schema_version"), "An unconfirmed retry cannot change existing unversioned tables");
            data = await FormAsync(); data["ConfirmExistingDatabase"] = "true";
            var confirmedUpgrade = await SendAsync("?handler=Initialize", data);
            check(confirmedUpgrade.StatusCode == HttpStatusCode.Redirect && finalDb.CheckSchema() &&
                finalDb.Accounts.Select<CmsUser>().Where(u => u.Id == shared.Id).First().PasswordHash == originalPasswordHash &&
                finalDb.Site.Select<Post>().Count() == 1,
                "Confirmation restores the account version table and keeps accounts, passwords and posts");

            // Existing tables no longer need classification as a legacy or current application database.
            await StopAsync();
            await new MySqlCommand($"CREATE DATABASE `{databases[3]}` CHARACTER SET utf8mb4", server).ExecuteNonQueryAsync();
            await new MySqlCommand($"CREATE TABLE `{databases[3]}`.legacy_data (id INT PRIMARY KEY)", server).ExecuteNonQueryAsync();
            connection.Database = databases[3];
            using (var legacySettings = JsonDocument.Parse(File.ReadAllText(Path.Combine(storage, "setup.json"))))
            {
                var settings = JsonSerializer.Deserialize<Dictionary<string, object>>(legacySettings.RootElement.GetRawText())!;
                settings["ConnectionStrings"] = new { Accounts = connection.ConnectionString, Site = connection.ConnectionString };
                File.WriteAllText(Path.Combine(storage, "setup.json"), JsonSerializer.Serialize(settings));
            }
            await StartAsync();
            html = await client.GetStringAsync("/Setup");
            using var legacyDb = Database(databases[3]);
            check(legacyDb.Site.DbFirst.ExistsTable("legacy_data") && !legacyDb.Site.DbFirst.ExistsTable("schema_version") &&
                !string.IsNullOrWhiteSpace(parser.ParseDocument(html).QuerySelector("form.setup-form")?.GetAttribute("data-confirm")),
                "Startup waits for confirmation when an existing database has no version table");
            data = await FormAsync(); data["ConfirmExistingDatabase"] = "true";
            await SendAsync("?handler=Initialize", data);
            check(legacyDb.CheckSchema() && legacyDb.Site.DbFirst.ExistsTable("legacy_data"),
                "Confirmed initialization adds the application's schema without deleting unrelated tables");
            data = await FormAsync();
            await SendAsync("?handler=Forget", data);
            check(parser.ParseDocument(await client.GetStringAsync("/Setup")).QuerySelector("input[name=Token]") != null, "Forget authorization returns the browser to the token form");
            await StopAsync();
            File.Delete(Path.Combine(storage, "setup.json"));
            connection.Database = databases[4];
            start.Environment["ConnectionStrings__Site"] = connection.ConnectionString;
            start.Environment["ConnectionStrings__Accounts"] = connection.ConnectionString;
            await StartAsync();
            using var automaticDb = Database(databases[4]);
            check(automaticDb.CheckSchema() && !automaticDb.Site.Select<SiteSettings>().Any() && (await client.GetAsync("/Setup")).StatusCode == HttpStatusCode.OK,
                "Environment-only deployment automatically creates a new database and its schema on startup, even with an old login cookie");
        }
        finally
        {
            await StopAsync();
            foreach (var name in databases)
                await new MySqlCommand($"DROP DATABASE IF EXISTS `{name}`", server).ExecuteNonQueryAsync();
        }
    }
}
