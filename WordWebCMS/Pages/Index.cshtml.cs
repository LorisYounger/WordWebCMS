using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WordWebCMS.Services;

namespace WordWebCMS.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationCache _cache;
        private readonly ILogger<IndexModel> _logger;

        public string HeaderHtml { get; set; } = "";
        public string ContentPageHtml { get; set; } = "";
        public string NavLinksHtml { get; set; } = "";
        public string SecondaryHtml { get; set; } = "";
        public string FooterHtml { get; set; } = "";
        public string PageTitle { get; set; } = "";

        public IndexModel(ApplicationCache cache, ILogger<IndexModel> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public IActionResult OnGet(string? rootPath, int? page, string? @class)
        {
            try
            {
                // Check if NomalIndex is set and redirect to specific post
                if (Setting.NomalIndex != -1 && page == null && @class == null)
                {
                    return Redirect(Setting.WebsiteURL + "/Post?ID=" + Setting.NomalIndex.ToString());
                }
            }
            catch (Exception ex)
            {
                // If database connection error
                if (ex.Message.StartsWith("WWCMS"))
                {
                    if (ex.Message == "WWCMS:无法连接数据库")
                    {
                        return Redirect("/Setup?step=1");
                    }
                }
                throw;
            }

            // Header
            if (_cache["MasterHeader"] == null)
            {
                _cache["MasterHeader"] = SMaster.GetHeaderHTML();
            }
            HeaderHtml = _cache["MasterHeader"]?.ToString() ?? "";

            // Simplified content for now
            ContentPageHtml = @"<article class=""post"">
                <header class=""entry-header"">
                    <h1 class=""entry-title"">欢迎使用 WordWebCMS (.NET 8.0)</h1>
                </header>
                <div class=""entry-content"">
                    <p>项目已成功迁移到 ASP.NET Core 8.0!</p>
                    <p>此页面是Index的Razor Pages版本。完整功能正在迁移中。</p>
                </div>
            </article>";

            // Navigation links
            NavLinksHtml = "";

            // Secondary sidebar
            SecondaryHtml = SMaster.GetNoLoginHTML();
            
            // Footer
            if (_cache["MasterFooter"] == null)
            {
                _cache["MasterFooter"] = SMaster.GetFooterHTML();
            }
            FooterHtml = _cache["MasterFooter"]?.ToString() ?? "";

            // Page title
            PageTitle = Setting.WebTitle;
            HeaderHtml = HeaderHtml.Replace("<!--WWC:head-->", $"<title>{PageTitle}</title>");

            return Page();
        }
    }
}
