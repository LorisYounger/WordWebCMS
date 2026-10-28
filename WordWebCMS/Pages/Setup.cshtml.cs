using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using WordWebCMS.Data;
using WordWebCMS.Models;
using WordWebCMS.Services;

namespace WordWebCMS.Pages;

public class SetupModel(IConfiguration config) : PageModel
{
    [BindProperty, Required] public string Token { get; set; } = "";
    [BindProperty, Required, StringLength(64)] public string UserName { get; set; } = "";
    [BindProperty, Required, EmailAddress] public string Email { get; set; } = "";
    [BindProperty, Required, StringLength(128, MinimumLength = 12)] public string Password { get; set; } = "";
    [BindProperty, Required, StringLength(120)] public string Title { get; set; } = "我的博客";
    [BindProperty, Required, StringLength(500)] public string BaseUrl { get; set; } = "";
    public bool Configured => !string.IsNullOrWhiteSpace(config.GetConnectionString("Accounts")) && !string.IsNullOrWhiteSpace(config.GetConnectionString("Site"));
    public bool Ready { get; set; }
    public string Message { get; set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        if (!Configured) return Page();
        var db = HttpContext.RequestServices.GetRequiredService<CmsDb>();
        Ready = db.CheckSchema();
        if (Ready && await db.Site.Select<SiteSettings>().AnyAsync()) return RedirectToPage("/Index");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!Configured) return BadRequest();
        var expected = config["Setup:Token"];
        if (string.IsNullOrEmpty(expected) || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(Token ?? ""), System.Text.Encoding.UTF8.GetBytes(expected))) return Forbid();
        var db = HttpContext.RequestServices.GetRequiredService<CmsDb>();
        Ready = db.CheckSchema();
        if (!Ready) return BadRequest("请先运行 --initialize。");
        if (await db.Site.Select<SiteSettings>().AnyAsync()) return StatusCode(409, "安装已完成，入口已锁定。");
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
