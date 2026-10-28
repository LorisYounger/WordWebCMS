using System.Text.RegularExpressions;

namespace WordWebCMS.Services;

public static class ShortLinks
{
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
        { "category", "tag", "archive", "search", "author", "page", "fonts", "admin", "account", "setup", "error", "p", "media", "theme-assets", "css", "js", "language", "index", "post", "feed", "sitemap", "themeasset", "mediafile" };

    public static bool IsValid(string slug) => Regex.IsMatch(slug, "^[a-z0-9][a-z0-9-]{0,159}$") && !Reserved.Contains(slug);

    public static string FromTitle(string title, string fallback)
    {
        var slug = Regex.Replace(title.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        if (slug.Length > 140) slug = slug[..140].TrimEnd('-');
        return IsValid(slug) ? slug : fallback;
    }

    public static string Avatar(string username) => "https://avatar.exlb.net/" + Uri.EscapeDataString(username);
}
