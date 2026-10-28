using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WordWebCMS.Data;
using WordWebCMS.Models;
using WordWebCMS.Services;

namespace WordWebCMS.Pages.Admin;
public class SettingsModel(CmsDb db) : PageModel
{
    [BindProperty] public SiteSettings Settings { get; set; } = new();
    [BindProperty] public List<string> SelectedLanguages { get; set; } = [];
    [BindProperty] public Dictionary<string, SiteTranslation> Translations { get; set; } = [];
    public async Task OnGetAsync()
    {
        Settings = await db.Site.Select<SiteSettings>().FirstAsync();
        Settings.Language = UiText.Normalize(Settings.Language);
        SelectedLanguages = UiText.Enabled(Settings).Select(c => c.Name).ToList();
        Translations = ContentLanguages.Read<SiteTranslation>(Settings.Translations);
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return BadRequest(UiText.T("表单字段无效。"));
        Settings.AvatarUrl = (Settings.AvatarUrl ?? "").Trim();
        Settings.AvatarUserName = (Settings.AvatarUserName ?? "").Trim();
        Settings.Language = UiText.Normalize(Settings.Language);
        SelectedLanguages = SelectedLanguages.Select(UiText.Normalize).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (SelectedLanguages.Count is < 1 or > 30 || SelectedLanguages.Any(c => !UiText.Supports(c)) || !SelectedLanguages.Contains(Settings.Language))
            throw new InvalidDataException(UiText.T("请选择至少一种已安装语言，并启用默认语言。"));
        if (Translations.Any(t => !UiText.Supports(t.Key) || (t.Value.Title?.Length ?? 0) > 120 || (t.Value.Description?.Length ?? 0) > 500))
            throw new InvalidDataException(UiText.T("请检查站点译文的语言和长度。"));
        var existing = ContentLanguages.Read<SiteTranslation>((await db.Site.Select<SiteSettings>().FirstAsync()).Translations);
        foreach (var translation in Translations) existing[translation.Key] = translation.Value;
        Settings.Translations = ContentLanguages.Write(existing);
        Settings.EnabledLanguages = string.Join(',', SelectedLanguages);
        if (!Enum.IsDefined(Settings.Registration) || Settings.AvatarUserName.Length is < 1 or > 64 ||
            Settings.AvatarUrl.Length > 1000 || Settings.AvatarUrl.Length > 0 && !MarkdownService.IsWebUrl(Settings.AvatarUrl))
            throw new InvalidDataException(UiText.T("请检查站点语言、注册模式和头像地址。"));
        if (string.IsNullOrWhiteSpace(Settings.Title) || Settings.Title.Length > 120 || (Settings.Description?.Length ?? 0) > 500 ||
            !Uri.TryCreate(Settings.BaseUrl, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https") ||
            url.AbsolutePath != "/" || url.Query != "" || url.Fragment != "" || url.UserInfo != "" || Settings.BaseUrl.Length > 500)
            throw new InvalidDataException(UiText.T("请检查站点名称、描述和公开根地址。"));
        await db.Site.Update<SiteSettings>().Where(s => s.Id == 1).Set(s => s.Title, Settings.Title.Trim())
            .Set(s => s.Description, Settings.Description ?? "").Set(s => s.BaseUrl, Settings.BaseUrl.TrimEnd('/'))
            .Set(s => s.AvatarUrl, Settings.AvatarUrl).Set(s => s.AvatarUserName, Settings.AvatarUserName)
            .Set(s => s.Language, Settings.Language).Set(s => s.Registration, Settings.Registration)
            .Set(s => s.EnabledLanguages, Settings.EnabledLanguages).Set(s => s.Translations, Settings.Translations)
            .Set(s => s.CommentsEnabled, Settings.CommentsEnabled).Set(s => s.ModerateComments, Settings.ModerateComments).ExecuteAffrowsAsync();
        TempData["Saved"] = true;
        return RedirectToPage();
    }
}
