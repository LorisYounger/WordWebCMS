using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using WordWebCMS.Data;
using WordWebCMS.Models;
using WordWebCMS.Services;
using MySqlConnector;

namespace WordWebCMS.Pages;

public class SetupModel(IConfiguration config, SetupService setup, CmsDb db) : PageModel
{
    [BindProperty] public string Token { get; set; } = "";
    [BindProperty] public DatabaseInput SiteDatabase { get; set; } = new();
    [BindProperty] public DatabaseInput AccountsDatabase { get; set; } = new() { Name = "wordwebcms_accounts" };
    [BindProperty] public bool UseSeparateAccounts { get; set; }
    [BindProperty] public bool ConfirmExistingDatabase { get; set; }
    [BindProperty, Required, StringLength(64)] public string UserName { get; set; } = "";
    [BindProperty, Required, EmailAddress] public string Email { get; set; } = "";
    [BindProperty, Required, StringLength(128, MinimumLength = 12)] public string Password { get; set; } = "";
    [BindProperty, Required, StringLength(120)] public string Title { get; set; } = "我的博客";
    [BindProperty, Required, StringLength(500)] public string BaseUrl { get; set; } = "";
    private bool authorizedThisRequest;
    public bool Authorized => authorizedThisRequest || setup.IsAuthorized(HttpContext);
    public bool Configured => setup.Configured;
    public bool Ready => setup.SchemaReady;
    public bool Installed => setup.Installed;
    public bool RequiresConfirmation => setup.RequiresConfirmation;
    public string DatabaseError => ModelState.ErrorCount == 0 ? setup.Error : "";
    public string Message { get; set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        await setup.Gate.WaitAsync(HttpContext.RequestAborted);
        try
        {
            setup.Refresh();
            if (Installed && Ready) return RedirectToPage("/Index");
            if (Authorized) LoadConnections();
            return Page();
        }
        finally { setup.Gate.Release(); }
    }

    public Task<IActionResult> OnPostAuthorizeAsync() => ExecuteAsync(() =>
    {
        ModelState.Clear();
        return Task.FromResult<IActionResult>(Page());
    }, installationOnly: false);

    public IActionResult OnPostForget()
    {
        setup.ForgetAuthorization(HttpContext);
        return RedirectToPage();
    }

    public Task<IActionResult> OnPostConfigureAsync() => ExecuteAsync(() =>
    {
        // Keep database binding errors (for example a non-numeric port), but ignore the later account form.
        foreach (var key in ModelState.Keys.Where(key => !key.StartsWith(nameof(SiteDatabase) + ".") &&
            !(UseSeparateAccounts && key.StartsWith(nameof(AccountsDatabase) + "."))).ToList()) ModelState.Remove(key);
        if (!ModelState.IsValid) return Task.FromResult<IActionResult>(Page());
        setup.Step = "读取数据库设置";
        var site = SiteDatabase.ToConnectionString(config.GetConnectionString("Site"));
        var accounts = UseSeparateAccounts ? AccountsDatabase.ToConnectionString(config.GetConnectionString("Accounts")) : site;
        setup.SaveConnections(accounts, site);
        // Connecting an existing site must never create another administrator or welcome post.
        if (!Ready) setup.Initialize(ConfirmExistingDatabase);
        LoadConnections();
        if (RequiresConfirmation) return Task.FromResult<IActionResult>(Page());
        Message = Installed ? "数据库已就绪，可以进入站点。" : "数据库已就绪，请继续填写站点和管理员信息。";
        return Task.FromResult<IActionResult>(Installed && Ready ? RedirectToPage("/Index") : Page());
    }, loadConnections: false);

    public Task<IActionResult> OnPostInitializeAsync() => ExecuteAsync(() =>
    {
        ModelState.Clear();
        if (!Configured) ModelState.AddModelError("", UiText.T("请先填写数据库连接信息。"));
        else
        {
            if (!Ready) setup.Initialize(ConfirmExistingDatabase);
            if (RequiresConfirmation) return Task.FromResult<IActionResult>(Page());
            Message = "数据库已就绪，请继续填写站点和管理员信息。";
            if (Installed && Ready) return Task.FromResult<IActionResult>(RedirectToPage("/Index"));
        }
        return Task.FromResult<IActionResult>(Page());
    }, installationOnly: false);

    public Task<IActionResult> OnPostAsync() => ExecuteAsync(CompleteAsync);

    private void LoadConnections()
    {
        SiteDatabase = DatabaseInput.FromConnectionString(config.GetConnectionString("Site"));
        AccountsDatabase = DatabaseInput.FromConnectionString(config.GetConnectionString("Accounts"));
        if (!Configured) AccountsDatabase.Name = "wordwebcms_accounts";
        UseSeparateAccounts = Configured && config.GetConnectionString("Site") != config.GetConnectionString("Accounts");
    }

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action, bool installationOnly = true, bool loadConnections = true)
    {
        if (!setup.Authorize(HttpContext, Token))
        {
            ModelState.Clear();
            ModelState.AddModelError(nameof(Token), UiText.T("Token 不正确，请重新复制启动日志中的 Setup Token。"));
            Response.StatusCode = 403;
            return Page();
        }
        authorizedThisRequest = true;
        await setup.Gate.WaitAsync(HttpContext.RequestAborted);
        try
        {
            setup.Refresh();
            if (installationOnly && Installed) return StatusCode(409, UiText.T("站点已安装，无法再次创建管理员或更改数据库连接。"));
            if (loadConnections) LoadConnections();
            return await action();
        }
        catch (InvalidDataException ex)
        {
            ModelState.Clear();
            ModelState.AddModelError("", ex.Message);
            return Page();
        }
        catch (Exception ex) when (ex is MySqlException or ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            ModelState.Clear();
            ModelState.AddModelError("", setup.ReportFailure(ex, SiteDatabase.Password, AccountsDatabase.Password, Password, Token));
            return Page();
        }
        finally { setup.Gate.Release(); }
    }

    private async Task<IActionResult> CompleteAsync()
    {
        setup.Step = "创建站点与管理员";
        foreach (var key in ModelState.Keys.Where(key => key.StartsWith(nameof(SiteDatabase) + ".") ||
            key.StartsWith(nameof(AccountsDatabase) + ".")).ToList()) ModelState.Remove(key);
        if (!Ready)
        {
            ModelState.Clear();
            ModelState.AddModelError("", UiText.T("请先保存数据库连接信息。"));
            return Page();
        }
        if (!ModelState.IsValid) return Page();
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https") || url.AbsolutePath != "/" || url.Query != "" || url.Fragment != "" || url.UserInfo != "")
        { ModelState.AddModelError(nameof(BaseUrl), "请填写站点根地址，例如 https://blog.example.com。"); return Page(); }
        var accounts = HttpContext.RequestServices.GetRequiredService<AccountService>();
        var email = Email.Trim().ToLowerInvariant();
        var account = await db.Accounts.Select<CmsUser>().Where(u => u.Email == email).FirstAsync();
        if (account != null)
        {
            var verified = await accounts.CheckPasswordAsync(email, Password);
            if (verified == null || !verified.EmailConfirmed)
            { ModelState.AddModelError("", "这个邮箱已有共享账号。请填写该账号的原密码，并确认邮箱已验证。"); return Page(); }
            account = verified;
        }
        else account = await accounts.CreateAsync(UserName, email, Password, true);
        db.Site.Transaction(() =>
        {
            // Singleton primary key is the installation lock, including concurrent setup requests.
            db.Site.Insert(new SiteSettings { Title = Title.Trim(), BaseUrl = BaseUrl.TrimEnd('/') }).ExecuteAffrows();
            db.Site.Insert(new SiteUserRole { UserId = account.Id, Role = SiteRole.Administrator }).ExecuteAffrows();

            // Keep the original welcome article, using the new draft/published revision model.
            var categoryId = db.Site.Insert(new Category { Name = "未分类" }).ExecuteIdentity();
            var post = new Post { AuthorId = account.Id, Slug = "helloworld", IsPublic = true, PublishedUtc = DateTime.UtcNow };
            post.Id = db.Site.Insert(post).ExecuteIdentity();
            var revision = new PostRevision
            {
                PostId = post.Id, EditorId = account.Id, State = RevisionState.Published,
                Title = "你好,世界 又一个WWCMS站点", Slug = post.Slug, CategoryId = categoryId,
                Summary = "一篇系统生成的默认文章\n这是系统自动生成的演示文章。编辑或者删除它，然后开始您的博客！",
                Markdown = """
                    ## 欢迎使用 WordWebCMS。
                    这是系统自动生成的演示文章。编辑或者删除它，然后开始您的博客！

                    ## WordWebCMS支持Markdown
                    **Markdown**是一种轻量级的「标记语言」
                    **Markdown**是一种可以使用普通文本编辑器编写的标记语言，通过简单的标记语法，它可以使普通文本内容具有一定的*格式*。它允许人们使用易读易写的纯文本格式编写文档，然后转换成格式丰富的HTML页面

                    ## 关于WordWebCMS
                    由[洛里斯杨远](https://zoujin.exlb.org)编写的一款内容管理网站
                    项目地址:[https://github.com/LorisYounger/WordWebCMS](https://github.com/LorisYounger/WordWebCMS)
                    """
            };
            revision.Id = db.Site.Insert(revision).ExecuteIdentity();
            db.Site.Insert(new PostDraft
            {
                PostId = post.Id, Title = revision.Title, Slug = revision.Slug, Summary = revision.Summary,
                Markdown = revision.Markdown, CategoryId = categoryId, AllowComments = revision.AllowComments
            }).ExecuteAffrows();
            db.Site.Update<Post>().Set(p => p.PublishedRevisionId, revision.Id).Where(p => p.Id == post.Id).ExecuteAffrows();
        });
        setup.Refresh();
        await accounts.SignInAsync(HttpContext, account, false);
        setup.ForgetAuthorization(HttpContext);
        return RedirectToPage("/Admin/Index");
    }

    public sealed class DatabaseInput
    {
        [Required, StringLength(255)] public string Server { get; set; } = "localhost";
        [Range(1, 65535)] public int Port { get; set; } = 3306;
        [Required, StringLength(64)] public string Name { get; set; } = "wordwebcms";
        [Required, StringLength(128)] public string UserName { get; set; } = "";
        [StringLength(4096)] public string Password { get; set; } = "";
        public bool ClearPassword { get; set; }
        [EnumDataType(typeof(MySqlSslMode))] public MySqlSslMode SslMode { get; set; } = MySqlSslMode.Preferred;

        public static DatabaseInput FromConnectionString(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return new();
            try
            {
                var parsed = new MySqlConnectionStringBuilder(value);
                return new() { Server = parsed.Server, Port = (int)parsed.Port, Name = parsed.Database,
                    UserName = parsed.UserID, SslMode = parsed.SslMode };
            }
            catch (ArgumentException) { return new(); }
        }

        public string ToConnectionString(string? previous)
        {
            MySqlConnectionStringBuilder parsed;
            try { parsed = new(previous ?? ""); }
            catch (ArgumentException) { parsed = new(); }
            var sameAccount = parsed.Server.Equals(Server.Trim(), StringComparison.OrdinalIgnoreCase) &&
                parsed.Port == Port && parsed.UserID == UserName.Trim();
            if (ClearPassword || !sameAccount) parsed.Password = "";
            if (!string.IsNullOrEmpty(Password)) parsed.Password = Password;
            parsed.Server = Server.Trim();
            parsed.Port = (uint)Port;
            parsed.Database = Name.Trim();
            parsed.UserID = UserName.Trim();
            parsed.SslMode = SslMode;
            return parsed.ConnectionString;
        }
    }
}
