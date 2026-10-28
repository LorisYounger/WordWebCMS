using System.Globalization;

namespace WordWebCMS.Services;

public static class PublicUrls
{
    public static string Category(string? name) => string.IsNullOrEmpty(name) ? "" : "/category/" + Uri.EscapeDataString(name);
    public static string Tag(string name) => "/tag/" + Uri.EscapeDataString(name);
    public static string Author(string name) => "/author/" + Uri.EscapeDataString(name);
    public static string Archive(DateTime? date) => date.HasValue ? $"/archive/{date.Value.Year}/{date.Value.Month}" : "";
    public static string List(string? category, string? tag, int? year, int? month, string? search, string? author, int page)
    {
        var path = Category(category);
        if (!string.IsNullOrEmpty(tag)) path += Tag(tag);
        if (year.HasValue) path += "/archive/" + year + (month.HasValue ? "/" + month : "");
        if (!string.IsNullOrEmpty(search)) path += "/search/" + Uri.EscapeDataString(search);
        if (!string.IsNullOrEmpty(author)) path += Author(author);
        if (page > 1) path += "/page/" + page;
        return path.Length == 0 ? "/" : path;
    }

    // MySQL DATETIME values are stored in UTC, but can be read with Kind=Unspecified.
    public static string Utc(DateTime? value) => value.HasValue
        ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture) : "";
}
