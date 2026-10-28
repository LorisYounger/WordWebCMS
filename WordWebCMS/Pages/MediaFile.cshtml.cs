using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WordWebCMS.Data;
using WordWebCMS.Models;
using WordWebCMS.Services;

namespace WordWebCMS.Pages;
public class MediaFileModel(CmsDb db, StoragePaths paths) : PageModel
{
    public async Task<IActionResult> OnGetAsync(string file)
    {
        var media = await db.Site.Select<Media>().Where(m => m.FileName == file).FirstAsync();
        if (media == null || Path.GetFileName(file) != file) return NotFound();
        var path = Path.Combine(paths.Media, media.FileName);
        if (!System.IO.File.Exists(path)) return NotFound();
        if (media.ContentType == "application/pdf") return PhysicalFile(path, media.ContentType, media.OriginalName);
        return PhysicalFile(path, media.ContentType);
    }
}
