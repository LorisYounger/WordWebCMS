using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WordWebCMS.Data;
using WordWebCMS.Models;
using WordWebCMS.Services;

namespace WordWebCMS.Pages.Admin;
[RequestSizeLimit(11 * 1024 * 1024)]
public class MediaModel(CmsDb db, StoragePaths paths) : PageModel
{
    public List<Media> Items { get; set; } = [];
    [BindProperty(SupportsGet = true)] public int P { get; set; } = 1;
    public async Task OnGetAsync()
    {
        P = Math.Clamp(P, 1, 100000);
        var userId = AccountService.UserId(User);
        Items = await db.Site.Select<Media>().WhereIf(!AccountService.CanEditAll(User), m => m.UserId == userId)
            .OrderByDescending(m => m.Id).Page(P, 24).ToListAsync();
    }
    public async Task<IActionResult> OnPostUploadAsync(IFormFile? upload, string? altText)
    {
        if (upload == null || upload.Length == 0 || upload.Length > 10 * 1024 * 1024 || (altText?.Length ?? 0) > 300)
            return BadRequest(UiText.T("请选择不大于 10 MB 的文件，替代文本最多 300 字符。"));
        var ext = Path.GetExtension(upload.FileName).ToLowerInvariant();
        var types = new Dictionary<string, string> { [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".gif"] = "image/gif", [".webp"] = "image/webp", [".pdf"] = "application/pdf" };
        if (!types.TryGetValue(ext, out var type)) return BadRequest(UiText.T("支持 PNG、JPEG、GIF、WebP 和 PDF。"));
        await using var input = upload.OpenReadStream();
        var header = new byte[12];
        var read = await input.ReadAtLeastAsync(header, 12, false);
        bool valid = read >= 12 && ext switch {
            ".png" => header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".jpg" or ".jpeg" => header[0] == 255 && header[1] == 216 && header[2] == 255,
            ".gif" => System.Text.Encoding.ASCII.GetString(header, 0, 6) is "GIF87a" or "GIF89a",
            ".webp" => System.Text.Encoding.ASCII.GetString(header, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(header, 8, 4) == "WEBP",
            ".pdf" => System.Text.Encoding.ASCII.GetString(header, 0, 5) == "%PDF-", _ => false
        };
        if (!valid) return BadRequest(UiText.T("文件内容与扩展名不匹配。"));
        var media = new Media { FileName = Guid.NewGuid().ToString("N") + ext, OriginalName = Path.GetFileName(upload.FileName),
            ContentType = type, AltText = altText ?? "", Size = upload.Length, UserId = AccountService.UserId(User) };
        if (media.OriginalName.Length > 255) return BadRequest(UiText.T("文件名过长。"));
        var target = Path.Combine(paths.Media, media.FileName);
        try
        {
            await using (var output = System.IO.File.Create(target))
            { await output.WriteAsync(header.AsMemory(0, read)); await input.CopyToAsync(output); }
            await db.Site.Insert(media).ExecuteAffrowsAsync();
        }
        catch { System.IO.File.Delete(target); throw; }
        return RedirectToPage();
    }
    public async Task<IActionResult> OnPostAltAsync(long id, string? altText)
    {
        if ((altText?.Length ?? 0) > 300) return BadRequest();
        var userId = AccountService.UserId(User);
        await db.Site.Update<Media>().Where(m => m.Id == id).WhereIf(!AccountService.CanEditAll(User), m => m.UserId == userId)
            .Set(m => m.AltText, altText ?? "").ExecuteAffrowsAsync();
        return RedirectToPage(new { P });
    }
    public async Task<IActionResult> OnPostDeleteAsync(long id)
    {
        var media = await db.Site.Select<Media>().Where(m => m.Id == id).FirstAsync();
        if (media == null) return NotFound();
        if (!AccountService.CanEditAll(User) && media.UserId != AccountService.UserId(User)) return Forbid();
        System.IO.File.Delete(Path.Combine(paths.Media, media.FileName));
        await db.Site.Delete<Media>().Where(m => m.Id == id).ExecuteAffrowsAsync();
        return RedirectToPage(new { P });
    }
}
