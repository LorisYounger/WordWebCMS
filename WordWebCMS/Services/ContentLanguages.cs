using System.Text.Encodings.Web;
using System.Text.Json;
using WordWebCMS.Models;

namespace WordWebCMS.Services;

public class ContentTranslation
{
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Markdown { get; set; } = "";
    public string CoverUrl { get; set; } = "";
}

public class SiteTranslation
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string FooterMarkdown { get; set; } = "";
}

public static class ContentLanguages
{
    // Stored JSON is searchable Unicode. It is never emitted as raw HTML or executable JavaScript.
    private static readonly JsonSerializerOptions StorageJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    public static Dictionary<string, T> Read<T>(string? json)
    {
        var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<Dictionary<string, T>>(json) ?? [])
        {
            var language = UiText.Normalize(entry.Key);
            if (!result.ContainsKey(language) || entry.Key.Equals(language, StringComparison.OrdinalIgnoreCase)) result[language] = entry.Value;
        }
        return result;
    }
    public static string Write<T>(Dictionary<string, T> translations) => JsonSerializer.Serialize(translations, StorageJson);
    private static string Pick<T>(Dictionary<string, T> translations, string defaultLanguage, string original, Func<T, string> field)
    {
        foreach (var language in UiText.Candidates(UiText.Language))
        {
            // Base fields are the site's default-language content.
            if (language.Equals(UiText.Normalize(defaultLanguage), StringComparison.OrdinalIgnoreCase)) return original;
            if (translations.TryGetValue(language, out var translation) && !string.IsNullOrWhiteSpace(field(translation))) return field(translation);
        }
        return original;
    }

    public static ContentTranslation Display(PostContent content, SiteSettings settings)
    {
        var translations = Read<ContentTranslation>(content.Translations);
        return new ContentTranslation { Title = Pick(translations, settings.Language, content.Title, t => t.Title), Summary = Pick(translations, settings.Language, content.Summary, t => t.Summary),
            Markdown = Pick(translations, settings.Language, content.Markdown, t => t.Markdown), CoverUrl = Pick(translations, settings.Language, content.CoverUrl, t => t.CoverUrl) };
    }

    public static SiteTranslation Display(SiteSettings settings)
    {
        var translations = Read<SiteTranslation>(settings.Translations);
        return new SiteTranslation { Title = Pick(translations, settings.Language, settings.Title, t => t.Title), Description = Pick(translations, settings.Language, settings.Description, t => t.Description),
            FooterMarkdown = Pick(translations, settings.Language, settings.FooterMarkdown, t => t.FooterMarkdown) };
    }
}
