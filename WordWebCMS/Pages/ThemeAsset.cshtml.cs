using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.StaticFiles;
using System.Text.RegularExpressions;
using WordWebCMS.Services;

namespace WordWebCMS.Pages;
public class ThemeAssetModel(StoragePaths paths) : PageModel
{
    public IActionResult OnGet(string theme, string? file)
    {
        if (!Regex.IsMatch(theme, "^[a-z0-9][a-z0-9-]{0,63}$") || file == null || !file.StartsWith("assets/", StringComparison.Ordinal) ||
            !Regex.IsMatch(file, "^[a-zA-Z0-9_./-]+$") || file.Split('/').Any(s => s is ".." or "." or "") ||
            !new[] { ".css", ".js", ".png", ".jpg", ".jpeg", ".webp", ".gif", ".woff2" }.Contains(Path.GetExtension(file))) return NotFound();
        var path = Path.Combine(paths.Themes, theme, file);
        if (!System.IO.File.Exists(path)) return NotFound();
        new FileExtensionContentTypeProvider().TryGetContentType(path, out var type);
        return PhysicalFile(path, type ?? "application/octet-stream");
    }
}
