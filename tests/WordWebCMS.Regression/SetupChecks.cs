using System.Diagnostics;
using System.Net;
using System.Text.Json;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using WordWebCMS.Data;
using WordWebCMS.Models;
using WordWebCMS.Services;

internal static class SetupChecks
{
    public static async Task RunAsync(string root, string connectionString, Action<bool, string> check)
    {
        var prefix = "wwcms_setup_" + Guid.NewGuid().ToString("N");
        var databases = new[] { prefix + "_web", prefix + "_cli" };
        var connection = new MySqlConnectionStringBuilder(connectionString) { Database = "" };
        using var server = new MySqlConnection(connection.ConnectionString);
        await server.OpenAsync();
        var storage = Path.Combine(root, "artifacts", prefix);
        var baseUrl = "http://127.0.0.1:5194";
        Process? web = null;
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
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() })
            { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(30) };
        async Task StartAsync()
        {
            web = Process.Start(start)!;
            web.OutputDataReceived += (_, _) => { }; web.ErrorDataReceived += (_, _) => { };
            web.BeginOutputReadLine(); web.BeginErrorReadLine();
            for (var attempt = 0; ; attempt++)
            {
                try { await client.GetAsync("/Setup"); break; }
                catch (HttpRequestException) when (attempt < 50 && !web.HasExited) { await Task.Delay(200); }
            }
            check(!web.HasExited, "Setup host starts successfully");
        }
        async Task StopAsync()
        {
            if (web != null && !web.HasExited) { web.Kill(entireProcessTree: true); await web.WaitForExitAsync(); }
            web?.Dispose(); web = null;
        }
        async Task<Dictionary<string, string>> FormAsync(string token)
        {
            var document = parser.ParseDocument(await client.GetStringAsync("/Setup"));
            return new() { ["__RequestVerificationToken"] = document.QuerySelector("input[name=__RequestVerificationToken]")!.GetAttribute("value")!, ["Token"] = token };
        }
        async Task<HttpResponseMessage> SendAsync(string handler, Dictionary<string, string> data) =>
            await client.PostAsync("/Setup" + handler, new FormUrlEncodedContent(data));
        string ReadToken() => JsonDocument.Parse(File.ReadAllText(Path.Combine(storage, "setup.json"))).RootElement.GetProperty("Setup").GetProperty("Token").GetString()!;
        try
        {
            foreach (var name in databases)
                await new MySqlCommand($"CREATE DATABASE `{name}` CHARACTER SET utf8mb4", server).ExecuteNonQueryAsync();
            await StartAsync();
            var token = ReadToken();
            check(token.Length == 64, "Blank setup token is generated in private persistent storage");
            var html = await client.GetStringAsync("/Setup");
            check(!html.Contains(token) && html.Contains("AccountsConnection") && html.Contains("handler=Initialize") && html.Contains("--initialize"), "Setup shows configuration and both initialization paths without revealing the token");
            check((await client.GetStringAsync("/Setup?culture=en")).Contains("Manual initialization (command line)"), "New setup steps are translated into English");
            await StopAsync();
            start.Environment["ConnectionStrings__Accounts"] = "UnsupportedKeyword=startup-secret";
            start.Environment["ConnectionStrings__Site"] = "UnsupportedKeyword=startup-secret";
            await StartAsync();
            var recovery = await client.GetStringAsync("/Setup");
            check(parser.ParseDocument(recovery).QuerySelector("[role=alert]") != null && !recovery.Contains("startup-secret"), "Malformed deployment connections keep setup available for recovery");
            await StopAsync();
            start.Environment["ConnectionStrings__Accounts"] = ""; start.Environment["ConnectionStrings__Site"] = "";
            await StartAsync();
            check((await SendAsync("?handler=Configure", new() { ["Token"] = token })).StatusCode == HttpStatusCode.BadRequest, "Setup configuration requires antiforgery protection");
            var data = await FormAsync("wrong-token");
            check((await SendAsync("?handler=Initialize", data)).StatusCode == HttpStatusCode.Forbidden, "Invalid token cannot initialize a database");
            data = await FormAsync(token);
            check((await SendAsync("?handler=Configure", data)).StatusCode == HttpStatusCode.OK && !File.ReadAllText(Path.Combine(storage, "setup.json")).Contains("ConnectionStrings"), "Empty connections are rejected without saving");
            data = await FormAsync(token);
            data["AccountsConnection"] = "Server=localhost;Database=invalid;User ID=test;UnsupportedKeyword=secret-marker";
            data["SiteConnection"] = data["AccountsConnection"];
            var invalid = await SendAsync("?handler=Configure", data);
            check(invalid.StatusCode == HttpStatusCode.OK && !(await invalid.Content.ReadAsStringAsync()).Contains("secret-marker"), "Malformed connections return a recoverable error without reflecting credentials");
            foreach (var name in databases)
            {
                connection.Database = name;
                data = await FormAsync(token);
                data["AccountsConnection"] = connection.ConnectionString;
                data["SiteConnection"] = connection.ConnectionString;
                var saved = await SendAsync("?handler=Configure", data);
                var savedHtml = parser.ParseDocument(await saved.Content.ReadAsStringAsync());
                check(saved.StatusCode == HttpStatusCode.OK && savedHtml.QuerySelector("input[name=AccountsConnection]")?.GetAttribute("value") is null or "", "Saved database credentials are never rendered into the form");
                check(ReadToken() == token, "Saving connections preserves the bootstrap token");
                var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                    ["ConnectionStrings:Accounts"] = connection.ConnectionString, ["ConnectionStrings:Site"] = connection.ConnectionString }).Build();
                using var database = new CmsDb(settings);
                if (name == databases[0])
                {
                    data = await FormAsync(token);
                    check((await SendAsync("?handler=Initialize", data)).StatusCode == HttpStatusCode.OK && database.CheckSchema(), "Token-protected web initialize creates the schema without restarting");
                    data = await FormAsync(token);
                    check((await SendAsync("?handler=Initialize", data)).StatusCode == HttpStatusCode.OK && database.CheckSchema(), "Repeated explicit initialization is safe before installation");
                }
                else
                {
                    // The CLI reads the exact saved setup file used by the running host.
                    var command = new ProcessStartInfo("dotnet") { WorkingDirectory = start.WorkingDirectory,
                        UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                    command.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "WordWebCMS.dll"));
                    command.ArgumentList.Add("--initialize");
                    foreach (var entry in start.Environment) command.Environment[entry.Key] = entry.Value;
                    using var initialize = Process.Start(command)!;
                    var output = initialize.StandardOutput.ReadToEndAsync(); var error = initialize.StandardError.ReadToEndAsync();
                    await initialize.WaitForExitAsync(); await output; await error;
                    check(initialize.ExitCode == 0 && database.CheckSchema(), "Manual CLI initialization uses the saved connections");
                    data = await FormAsync(token);
                    var rechecked = parser.ParseDocument(await (await SendAsync("?handler=Check", data)).Content.ReadAsStringAsync());
                    check(!rechecked.QuerySelector("fieldset")!.HasAttribute("disabled"), "Running host recognizes manual initialization without restarting");
                }
            }
            await StopAsync();
            await StartAsync();
            check(ReadToken() == token && !parser.ParseDocument(await client.GetStringAsync("/Setup")).QuerySelector("fieldset")!.HasAttribute("disabled"), "Connections and token survive a process restart");
            var finalConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:Accounts"] = connection.ConnectionString, ["ConnectionStrings:Site"] = connection.ConnectionString }).Build();
            using var finalDb = new CmsDb(finalConfig);
            var password = "Setup-" + Guid.NewGuid().ToString("N");
            var shared = await new AccountService(finalDb, new PasswordHasher<CmsUser>()).CreateAsync("shared-admin", "setup@example.test", password, true);
            data = await FormAsync(token);
            data["Title"] = "Setup regression"; data["BaseUrl"] = baseUrl;
            data["UserName"] = "shared-admin"; data["Email"] = shared.Email; data["Password"] = "incorrect-shared-password";
            check((await SendAsync("", data)).StatusCode == HttpStatusCode.OK && !finalDb.Site.Select<SiteSettings>().Any(), "Existing shared account requires its original verified password");
            data["Password"] = password;
            var completed = await SendAsync("", data);
            check(completed.StatusCode == HttpStatusCode.Redirect && completed.Headers.Location?.ToString() == "/Admin", "Setup completes and signs in the administrator");
            check(finalDb.Accounts.Select<CmsUser>().Count() == 1 && finalDb.Site.Select<SiteUserRole>().Where(r => r.UserId == shared.Id && r.Role == SiteRole.Administrator).Any(), "Setup reuses the shared account and grants the local administrator role");
            check(finalDb.Site.Select<Post>().Count() == 1 && (await client.GetAsync("/")).StatusCode == HttpStatusCode.OK, "New site is immediately accessible with one welcome post");
            data["__RequestVerificationToken"] = parser.ParseDocument(await client.GetStringAsync("/Admin"))
                .QuerySelector("input[name=__RequestVerificationToken]")!.GetAttribute("value")!;
            foreach (var handler in new[] { "", "?handler=Configure", "?handler=Initialize", "?handler=Check" })
                check((await SendAsync(handler, data)).StatusCode == HttpStatusCode.Conflict, "Completed setup locks handler " + handler);
        }
        finally
        {
            await StopAsync();
            foreach (var name in databases)
                await new MySqlCommand($"DROP DATABASE IF EXISTS `{name}`", server).ExecuteNonQueryAsync();
        }
    }
}
