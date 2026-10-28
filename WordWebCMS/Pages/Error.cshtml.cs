using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WordWebCMS.Data;
using WordWebCMS.Models;
using WordWebCMS.Services;

namespace WordWebCMS.Pages;

[IgnoreAntiforgeryToken]
public class ErrorModel(ILogger<ErrorModel> logger, IConfiguration config) : PageModel
{
    public int Code { get; set; }
    public string Message { get; set; } = "";
    public Task<IActionResult> OnPostAsync(int code = 500) => OnGetAsync(code);
    public async Task<IActionResult> OnGetAsync(int code = 500)
    {
        Code = code;
        Message = code switch { 404 => UiText.T("页面不存在"), 403 => UiText.T("没有访问权限"), 429 => UiText.T("请求过于频繁，请稍后再试"), _ => UiText.T("暂时无法完成请求") };
        Response.StatusCode = code;
        var exception = HttpContext.Features.Get<IExceptionHandlerFeature>();
        if (exception != null) logger.LogError(exception.Error, UiText.T("请求失败 {TraceId}"), HttpContext.TraceIdentifier);
        if (code != 500 && !string.IsNullOrWhiteSpace(config.GetConnectionString("Site")))
        {
            var db = HttpContext.RequestServices.GetRequiredService<CmsDb>();
            if (await db.Site.Select<SiteSettings>().AnyAsync())
                return await HttpContext.RequestServices.GetRequiredService<ThemeService>().PageAsync(db, HttpContext, "error.liquid",
                    new Dictionary<string, object?> { ["title"] = Message, ["code"] = code, ["message"] = Message }, code);
        }
        return Page();
    }
}
