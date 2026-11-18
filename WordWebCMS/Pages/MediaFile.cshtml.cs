using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WordWebCMS.Data;
using WordWebCMS.Models;
using WordWebCMS.Services;

namespace WordWebCMS.Pages;
public class MediaFileModel(CmsDb db, StoragePaths paths, IWebHostEnvironment env) : PageModel
{
    public async Task<IActionResult> OnGetAsync(string file)
    {
        if (string.IsNullOrEmpty(file) || !MediaFiles.ValidName(file)) return NotFound();
        var media = await db.Site.Select<Media>().Where(m => m.LinkName == file || m.LinkName == null && m.FileName == file).FirstAsync();
        if (media == null) return NotFound();
        var path = MediaFiles.PathFor(media, paths, env.WebRootPath);
        if (!System.IO.File.Exists(path)) return NotFound();
        var info = new FileInfo(path);
        // Custom names can be reused after deletion or editing, so they must revalidate.
        Response.Headers.CacheControl = media.LinkName == null ? "public, max-age=31536000, immutable" : "public, no-cache";
        return new PhysicalFileResult(path, media.ContentType)
        {
            FileDownloadName = media.ContentType == "application/pdf" ? media.OriginalName : null,
            LastModified = info.LastWriteTimeUtc,
            EntityTag = new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{info.LastWriteTimeUtc.Ticks:x}-{info.Length:x}\"", isWeak: true)
        };
    }
}
