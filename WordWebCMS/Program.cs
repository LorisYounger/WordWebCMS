using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using System.Threading.RateLimiting;
using WordWebCMS.Data;
using WordWebCMS.Models;
using WordWebCMS.Services;

var builder = WebApplication.CreateBuilder(args);
if (args.Contains("--local"))
{
    builder.Configuration.AddJsonFile(Path.GetFullPath("../Local/appsettings.local.json", builder.Environment.ContentRootPath), optional: false)
        .AddEnvironmentVariables().AddCommandLine(args);
}
var storage = Path.GetFullPath(builder.Configuration["Storage:Path"] ?? "App_Data", builder.Environment.ContentRootPath);
Directory.CreateDirectory(storage);
Directory.CreateDirectory(Path.Combine(storage, "media"));
Directory.CreateDirectory(Path.Combine(storage, "keys"));
// Operator-saved setup values survive restarts and override deployment connection strings.
builder.Configuration.AddJsonFile(Path.Combine(storage, "setup.json"), optional: true, reloadOnChange: false);
builder.Services.AddSingleton(new StoragePaths(storage));
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(storage, "keys")))
    .SetApplicationName(builder.Configuration["Site:CookieName"] ?? "WordWebCMS");
builder.Services.AddSingleton<CmsDb>();
builder.Services.AddSingleton<SetupService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<ContentService>();
builder.Services.AddSingleton<MarkdownService>();
builder.Services.AddSingleton<ThemeService>();
builder.Services.AddSingleton<MailService>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<MathChallenge>();
builder.Services.AddScoped<IPasswordHasher<CmsUser>, PasswordHasher<CmsUser>>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = builder.Configuration["Site:CookieName"] ?? "WordWebCMS";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Error/403";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.Events.OnValidatePrincipal = context =>
    {
        var setup = context.HttpContext.RequestServices.GetRequiredService<SetupService>();
        if (!setup.SchemaReady || !setup.Installed)
        {
            context.RejectPrincipal();
            return Task.CompletedTask;
        }
        return context.HttpContext.RequestServices.GetRequiredService<AccountService>().ValidateCookieAsync(context);
    };
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Write", p => p.RequireRole("Contributor", "Editor", "Administrator"));
    options.AddPolicy("Edit", p => p.RequireRole("Editor", "Administrator"));
    options.AddPolicy("Admin", p => p.RequireRole("Administrator"));
});
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AddPageRoute("/Post", "/{slug:regex(^[a-zA-Z0-9-]+$)}");
    foreach (var prefix in new[] { "category", "tag", "archive", "search", "author", "page" })
        options.Conventions.AddPageRoute("/Index", "/" + prefix + "/{**filters}");
    options.Conventions.AddPageRoute("/Post", "/{slug}/comments/{cp:int}");
    options.Conventions.AuthorizeFolder("/Admin", "Write");
    foreach (var page in new[] { "Comments", "Categories" })
        options.Conventions.AuthorizePage("/Admin/" + page, "Edit");
    foreach (var page in new[] { "Users", "Settings", "Themes", "Navigation" })
        options.Conventions.AuthorizePage("/Admin/" + page, "Admin");
}).AddMvcOptions(options =>
{
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    options.Filters.Add<CmsExceptionFilter>();
    options.Filters.Add<MathChallengeFilter>();
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("forms", context =>
    {
        var accountForm = context.Request.Path.StartsWithSegments("/Account") && !context.Request.Path.StartsWithSegments("/Account/Profile");
        return HttpMethods.IsPost(context.Request.Method)
        ? RateLimitPartition.GetFixedWindowLimiter((context.Connection.RemoteIpAddress?.ToString() ?? "unknown") +
            (accountForm ? ":account" : ":content"),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = accountForm ? 10 : 120,
                Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })
        : RateLimitPartition.GetNoLimiter("read");
    });
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var ip in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
        options.KnownProxies.Add(System.Net.IPAddress.Parse(ip));
});
var app = builder.Build();
if (args.Contains("--initialize"))
{
    app.Services.GetRequiredService<CmsDb>().InitializeSchema();
    app.Logger.LogInformation("数据库结构已初始化为版本 4；已有共享账号保持不变。请启动站点并访问 /Setup。");
    return;
}
var setup = app.Services.GetRequiredService<SetupService>();
setup.EnsureToken();
setup.InitializeOnStartup();
app.Services.GetRequiredService<ThemeService>().InitializeDefault();
UiText.Load(Path.Combine(app.Environment.ContentRootPath, "Languages"), Path.Combine(storage, "languages"));
var localization = new RequestLocalizationOptions {
    DefaultRequestCulture = new RequestCulture("zh-Hans"), SupportedCultures = UiText.Cultures.ToList(), SupportedUICultures = UiText.Cultures.ToList(),
    RequestCultureProviders = [new CustomRequestCultureProvider(async context => {
        var settings = setup.SchemaReady && !context.Request.Path.StartsWithSegments("/Error") && !context.Request.Path.StartsWithSegments("/Setup")
            ? await context.RequestServices.GetRequiredService<CmsDb>().Site.Select<SiteSettings>().FirstAsync() : null;
        context.Items["SiteSettings"] = settings;
        var enabled = UiText.Enabled(settings);
        var requested = context.Request.Query["culture"].ToString();
        var chosen = enabled.FirstOrDefault(c => c.Name.Equals(UiText.Normalize(requested), StringComparison.OrdinalIgnoreCase))?.Name;
        if (chosen == null)
        {
            var saved = CookieRequestCultureProvider.ParseCookieValue(context.Request.Cookies[CookieRequestCultureProvider.DefaultCookieName] ?? "")?.UICultures.FirstOrDefault().ToString();
            chosen = enabled.FirstOrDefault(c => c.Name.Equals(UiText.Normalize(saved ?? ""), StringComparison.OrdinalIgnoreCase))?.Name;
        }
        if (chosen == null)
        {
            var browserLanguages = context.Request.GetTypedHeaders().AcceptLanguage;
            chosen = browserLanguages?.Where(l => (l.Quality ?? 1) > 0).OrderByDescending(l => l.Quality ?? 1)
                .Select(l => UiText.Match(l.Value.ToString(), enabled)).FirstOrDefault(l => l != null);
        }
        return new ProviderCultureResult(chosen ?? UiText.Match(settings?.Language, enabled) ?? enabled[0].Name);
    })]
};
app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseExceptionHandler("/Error/500");
app.UseStatusCodePagesWithReExecute("/Error/{0}");
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    var path = context.Request.Path;
    if (path.StartsWithSegments("/Admin") || path.StartsWithSegments("/Account") || path.StartsWithSegments("/Setup"))
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; img-src 'self' https: http:; style-src 'self'; script-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    }
    await next();
});
app.UseStaticFiles();
app.UseRequestLocalization(localization);
app.UseRouting();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/Setup") || context.Request.Path.StartsWithSegments("/Error"))
    { await next(); return; }
    if (!setup.SchemaReady)
    { context.Response.Redirect("/Setup"); return; }
    if (context.Items["SiteSettings"] == null)
    { context.Response.Redirect("/Setup"); return; }
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages().RequireRateLimiting("forms");
app.Run();
