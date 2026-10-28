using System.Globalization;
using System.Text.Json;
using WordWebCMS.Models;

namespace WordWebCMS.Services;

public static class UiText
{
    private static readonly Dictionary<string, Dictionary<string, string>> Catalogs = new(StringComparer.OrdinalIgnoreCase);
    public static IReadOnlyList<CultureInfo> Cultures { get; private set; } = [];
    public static string Language => CultureInfo.CurrentUICulture.Name;
    public static string ClientJson => JsonSerializer.Serialize(new[] {
        "保存中…", "草稿已保存 · ", "保存失败：", "有未保存修改", "草稿版本冲突，请复制文字后刷新页面。",
        "预览失败，请检查登录状态后重试。", "正在回复评论 #"
    }.ToDictionary(key => key, T));

    // Load bundled dictionaries first, then operator overrides. Restart after changing a language pack.
    public static void Load(params string[] directories)
    {
        Catalogs.Clear();
        Catalogs["zh-Hans"] = new(StringComparer.Ordinal);
        foreach (var directory in directories)
        {
            Directory.CreateDirectory(directory);
            foreach (var file in Directory.EnumerateFiles(directory, "*.json").Order())
            {
                var culture = CultureInfo.GetCultureInfo(Normalize(Path.GetFileNameWithoutExtension(file)));
                if (string.IsNullOrEmpty(culture.Name)) throw new InvalidDataException("Language packs must use a named CultureInfo.");
                var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file))
                    ?? throw new InvalidDataException("Invalid language pack: " + Path.GetFileName(file));
                if (!Catalogs.TryGetValue(culture.Name, out var catalog)) Catalogs[culture.Name] = catalog = new(StringComparer.Ordinal);
                foreach (var entry in entries) catalog[entry.Key] = entry.Value;
            }
        }
        Cultures = Catalogs.Keys.Select(CultureInfo.GetCultureInfo).OrderBy(c => c.Name).ToArray();
    }

    public static string Normalize(string culture) => culture.Equals("zh-CN", StringComparison.OrdinalIgnoreCase) ? "zh-Hans" : culture;
    public static bool Supports(string? culture) => culture != null && Catalogs.ContainsKey(Normalize(culture));
    public static IReadOnlyList<CultureInfo> Enabled(SiteSettings? settings)
    {
        if (settings == null) return Cultures;
        var names = (settings.EnabledLanguages ?? "zh-Hans,en").Split(',', StringSplitOptions.RemoveEmptyEntries).Append(settings.Language).Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Cultures.Where(c => names.Contains(c.Name)).ToArray();
    }
    public static string Fallback(string language) => language.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh-Hans" : "en";

    public static IEnumerable<string> Candidates(string language)
    {
        CultureInfo culture;
        try { culture = CultureInfo.GetCultureInfo(Normalize(language)); }
        catch (CultureNotFoundException) { yield break; }
        for (; !string.IsNullOrEmpty(culture.Name); culture = culture.Parent) yield return Normalize(culture.Name);
        yield return Fallback(language);
    }

    public static string? Match(string? language, IReadOnlyList<CultureInfo> enabled) => string.IsNullOrWhiteSpace(language) || language == "*" ? null :
        Candidates(language).Select(name => enabled.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Name).FirstOrDefault(name => name != null);

    public static string T(string text)
    {
        foreach (var language in Candidates(Language))
            if (Catalogs.TryGetValue(language, out var catalog) && catalog.TryGetValue(text, out var translated) && !string.IsNullOrWhiteSpace(translated)) return translated;
        return text;
    }
    public static string Format(string text, params object?[] args) => string.Format(CultureInfo.CurrentCulture, T(text), args);
}
