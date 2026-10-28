using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using WordWebCMS.Data;
using WordWebCMS.Models;
using WordWebCMS.Services;
using MySqlConnector;

namespace WordWebCMS.Pages;

public class SetupModel(IConfiguration config, SetupService setup, CmsDb db, ILogger<SetupModel> logger) : PageModel
{
    [BindProperty] public string Token { get; set; } = "";
    [BindProperty, StringLength(4096)] public string AccountsConnection { get; set; } = "";
    [BindProperty, StringLength(4096)] public string SiteConnection { get; set; } = "";
    [BindProperty, Required, StringLength(64)] public string UserName { get; set; } = "";
    [BindProperty, Required, EmailAddress] public string Email { get; set; } = "";
    [BindProperty, Required, StringLength(128, MinimumLength = 12)] public string Password { get; set; } = "";
    [BindProperty, Required, StringLength(120)] public string Title { get; set; } = "我的博客";
    [BindProperty, Required, StringLength(500)] public string BaseUrl { get; set; } = "";
    public bool Configured => setup.Configured;
    public bool Ready => setup.SchemaReady;
    public bool Installed => setup.Installed;
    public string DatabaseError => setup.Error;
    public string Message { get; set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        await setup.Gate.WaitAsync(HttpContext.RequestAborted);
        try
        {
            setup.Refresh();
            return setup.Installed && Ready ? RedirectToPage("/Index") : Page();
        }
        finally { setup.Gate.Release(); }
    }

    public Task<IActionResult> OnPostConfigureAsync() => ExecuteAsync(() =>
    {
        ModelState.Clear();
        var accounts = string.IsNullOrWhiteSpace(AccountsConnection) ? config.GetConnectionString("Accounts") : AccountsConnection.Trim();
        var site = string.IsNullOrWhiteSpace(SiteConnection) ? config.GetConnectionString("Site") : SiteConnection.Trim();
        if (string.IsNullOrWhiteSpace(accounts) || string.IsNullOrWhiteSpace(site) || accounts.Length > 4096 || site.Length > 4096)
            ModelState.AddModelError("", UiText.T("请填写账号库和站点库的完整连接字符串。"));
        else
        {
            setup.SaveConnections(accounts, site);
            Message = "连接验证成功，配置已保存并立即生效。";
        }
        return Task.FromResult<IActionResult>(Page());
    });

    public Task<IActionResult> OnPostInitializeAsync() => ExecuteAsync(() =>
    {
        ModelState.Clear();
        if (!Configured) ModelState.AddModelError("", UiText.T("请先保存数据库连接。"));
        else
        {
            db.InitializeSchema();
            setup.Refresh();
            Message = "数据库初始化完成，可以填写站点和管理员信息。";
        }
        return Task.FromResult<IActionResult>(Page());
    });

    public Task<IActionResult> OnPostCheckAsync() => ExecuteAsync(() =>
    {
        ModelState.Clear();
        Message = Ready ? "数据库结构已就绪，可以完成安装。" : "数据库结构尚未就绪，请执行初始化或检查命令行执行结果。";
        return Task.FromResult<IActionResult>(Page());
    });

    public Task<IActionResult> OnPostAsync() => ExecuteAsync(CompleteAsync);

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        if (!setup.VerifyToken(Token ?? ""))
        {
            ModelState.Clear();
            ModelState.AddModelError(nameof(Token), UiText.T("安装令牌不正确。请从服务器配置中读取 Setup:Token。"));
            Response.StatusCode = 403;
            return Page();
        }
        await setup.Gate.WaitAsync(HttpContext.RequestAborted);
        try
        {
            setup.Refresh();
            if (setup.Installed) return StatusCode(409, "安装已完成，入口已锁定。");
            return await action();
        }
        catch (Exception ex) when (ex is MySqlException or ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            // Connection errors can contain credentials; do not return or log exception text.
            logger.LogWarning("Setup operation failed ({ErrorType}).", ex.GetType().Name);
            ModelState.Clear();
            ModelState.AddModelError("", UiText.T("操作失败，请检查连接字符串、数据库权限、数据库版本和配置目录的写入权限，然后重试。"));
            return Page();
        }
        finally { setup.Gate.Release(); }
    }

    private async Task<IActionResult> CompleteAsync()
    {
        if (!Ready)
        {
            ModelState.Clear();
            ModelState.AddModelError("", UiText.T("请先完成数据库初始化。"));
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
            { ModelState.AddModelError("", "共享账号已存在，请使用该已验证账号的正确密码。"); return Page(); }
            account = verified;
        }
        else account = await accounts.CreateAsync(UserName, email, Password, true);
        db.Site.Transaction(() =>
        {
            // Singleton primary key is the installation lock, including concurrent setup requests.
            db.Site.Insert(new SiteSettings { Title = Title.Trim(), BaseUrl = BaseUrl.TrimEnd('/'), AvatarUserName = account.UserName }).ExecuteAffrows();
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
        await accounts.SignInAsync(HttpContext, account, false);
        return RedirectToPage("/Admin/Index");
    }
}
