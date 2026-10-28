using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WordWebCMS.Data;
using WordWebCMS.Models;
using WordWebCMS.Services;

namespace WordWebCMS.Pages.Admin;

[RequestSizeLimit(2 * 1024 * 1024)]
public class EditModel(CmsDb db, ContentService content, MarkdownService markdown) : PageModel
{
    [BindProperty] public PostDraft Draft { get; set; } = new();
    [BindProperty(Name = "Translations")] public Dictionary<string, ContentTranslation> Translations { get; set; } = [];
    public IReadOnlyList<System.Globalization.CultureInfo> ContentCultures { get; set; } = [];
    public System.Globalization.CultureInfo DefaultCulture { get; set; } = System.Globalization.CultureInfo.GetCultureInfo("zh-Hans");
    public Post Post { get; set; } = new();
    public List<Category> Categories { get; set; } = [];
    public List<PostRevision> Revisions { get; set; } = [];
    public PostRevision? Selected { get; set; }
    public string Preview { get; set; } = "";
    [BindProperty(SupportsGet = true)] public int Rp { get; set; } = 1;
    public async Task OnGetAsync(long id, long? revision)
    {
        Post = content.RequireEditable(id, User);
        Draft = await db.Site.Select<PostDraft>().Where(d => d.PostId == id).FirstAsync();
        Translations = ContentLanguages.Read<ContentTranslation>(Draft.Translations);
        var settings = await db.Site.Select<SiteSettings>().FirstAsync();
        DefaultCulture = System.Globalization.CultureInfo.GetCultureInfo(UiText.Normalize(settings.Language));
        ContentCultures = UiText.Enabled(settings).Where(c => c.Name != DefaultCulture.Name).ToArray();
        Categories = await db.Site.Select<Category>().OrderBy(c => c.Name).ToListAsync();
        Rp = Math.Clamp(Rp, 1, 100000);
        Revisions = await db.Site.Select<PostRevision>().Where(r => r.PostId == id).OrderByDescending(r => r.Id).Page(Rp, 20).ToListAsync();
        if (revision.HasValue)
        {
            Selected = await db.Site.Select<PostRevision>().Where(r => r.Id == revision && r.PostId == id).FirstAsync();
            if (Selected != null) Preview = markdown.Render(Selected.Markdown);
        }
    }
    public IActionResult OnPost(long id, string action = "save")
    {
        if (!ModelState.IsValid) return BadRequest(UiText.T("表单字段无效。"));
        MergeTranslations(id);
        if (action is "convert-page" or "convert-article")
            db.Site.Transaction(() =>
            {
                var version = content.Save(id, Draft, User, "save");
                content.Convert(id, action == "convert-page" ? PostKind.Page : PostKind.Article, version, User);
            });
        else content.Save(id, Draft, User, action);
        return RedirectToPage(new { id });
    }
    public IActionResult OnPostAutosave(long id)
    {
        if (!ModelState.IsValid) return BadRequest(UiText.T("表单字段无效。"));
        MergeTranslations(id);
        var version = content.Save(id, Draft, User, "autosave");
        return new JsonResult(new { version, savedUtc = DateTime.UtcNow });
    }
    private void MergeTranslations(long id)
    {
        content.RequireEditable(id, User);
        var current = db.Site.Select<PostDraft>().Where(d => d.PostId == id).First();
        var translations = ContentLanguages.Read<ContentTranslation>(current.Translations);
        var enabled = UiText.Enabled(db.Site.Select<SiteSettings>().First()).Select(c => c.Name).ToHashSet();
        foreach (var translation in Translations)
        {
            if (!enabled.Contains(translation.Key)) throw new InvalidDataException(UiText.T("该内容语言尚未启用。"));
            translations[translation.Key] = translation.Value;
        }
        Draft.Translations = ContentLanguages.Write(translations);
    }

    public IActionResult OnPostPreview(long id, string? contentLanguage)
    {
        content.RequireEditable(id, User);
        var source = !string.IsNullOrEmpty(contentLanguage) && Translations.TryGetValue(contentLanguage, out var translation) ? translation.Markdown : Draft.Markdown;
        if (source?.Length > 500000) return BadRequest();
        return Content(markdown.Render(source ?? ""), "text/html; charset=utf-8");
    }
    public IActionResult OnPostReview(long id, long revisionId, bool approve)
    { content.Review(id, revisionId, approve, User); return RedirectToPage(new { id, revision = revisionId }); }
    public IActionResult OnPostRestore(long id, long revisionId, long version)
    { content.Restore(id, revisionId, version, User); return RedirectToPage(new { id }); }
}
