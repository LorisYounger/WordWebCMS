using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WordWebCMS.Data;
using WordWebCMS.Models;
using WordWebCMS.Services;

namespace WordWebCMS.Pages.Admin;
[RequestSizeLimit(21 * 1024 * 1024)]
public class ThemesModel(CmsDb db, ThemeService themes, StoragePaths paths) : PageModel
{
    public List<ThemeManifest> Themes { get; set; } = [];
    public string Current { get; set; } = "";
    public async Task OnGetAsync()
    {
        Current = (await db.Site.Select<SiteSettings>().FirstAsync()).Theme;
        foreach (var dir in Directory.EnumerateDirectories(paths.Themes))
        {
            var file = Path.Combine(dir, "theme.json");
            if (System.IO.File.Exists(file))
            {
                var theme = JsonSerializer.Deserialize<ThemeManifest>(await System.IO.File.ReadAllTextAsync(file), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (theme != null) Themes.Add(theme);
            }
        }
    }
    public async Task<IActionResult> OnPostUploadAsync(IFormFile? upload)
    {
        if (upload == null) return BadRequest(UiText.T("请选择 ZIP 文件。"));
        await themes.InstallAsync(upload);
        return RedirectToPage();
    }
    public async Task<IActionResult> OnPostActivateAsync(string theme)
    {
        if (!Regex.IsMatch(theme, "^[a-z0-9][a-z0-9-]{0,63}$") || !Directory.Exists(Path.Combine(paths.Themes, theme))) return BadRequest();
        // Check each page template at activation as well as at installation.
        foreach (var template in ThemeService.Templates.Where(t => t != "layout.liquid").Append("page.liquid"))
            await themes.RenderAsync(theme, template, new Dictionary<string, object?>());
        await db.Site.Update<SiteSettings>().Set(s => s.Theme, theme).Where(s => s.Id == 1).ExecuteAffrowsAsync();
        return RedirectToPage();
    }
}
