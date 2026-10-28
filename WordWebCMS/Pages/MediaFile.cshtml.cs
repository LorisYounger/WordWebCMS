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
        var info = new FileInfo(path);
        // Uploads get unique filenames and are never overwritten.
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return new PhysicalFileResult(path, media.ContentType)
        {
            FileDownloadName = media.ContentType == "application/pdf" ? media.OriginalName : null,
            LastModified = info.LastWriteTimeUtc,
            EntityTag = new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{info.LastWriteTimeUtc.Ticks:x}-{info.Length:x}\"", isWeak: true)
        };
    }
}
