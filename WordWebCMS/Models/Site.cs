using FreeSql.DataAnnotations;

namespace WordWebCMS.Models;

public enum RegistrationMode { Open, Closed, Approval }

[Table(Name = "site_settings")]
public class SiteSettings
{
    [Column(IsPrimary = true)] public int Id { get; set; } = 1;
    [Column(StringLength = 120)] public string Title { get; set; } = "WordWebCMS";
    [Column(StringLength = 500)] public string Description { get; set; } = "";
    [Column(StringLength = 500)] public string BaseUrl { get; set; } = "";
    [Column(StringLength = 64)] public string Theme { get; set; } = "default";
    public bool CommentsEnabled { get; set; } = true;
    public bool ModerateComments { get; set; } = true;
    public bool HideUserAvatars { get; set; }
    [Column(StringLength = -1)] public string FooterMarkdown { get; set; } = "";
    [Column(StringLength = 1000)] public string AvatarUrl { get; set; } = "";
    [Column(StringLength = 64)] public string Language { get; set; } = "zh-Hans";
    [Column(StringLength = 2000)] public string EnabledLanguages { get; set; } = "zh-Hans,en";
    [Column(StringLength = -1)] public string Translations { get; set; } = "{}";
    [Column(MapType = typeof(int))] public RegistrationMode Registration { get; set; }
}

[Table(Name = "media")]
public class Media
{
    [Column(IsPrimary = true, IsIdentity = true)] public long Id { get; set; }
    [Column(StringLength = 100)] public string FileName { get; set; } = "";
    [Column(StringLength = 255)] public string OriginalName { get; set; } = "";
    [Column(StringLength = 100)] public string ContentType { get; set; } = "";
    [Column(StringLength = 300)] public string AltText { get; set; } = "";
    public long Size { get; set; }
    public long UserId { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

[Table(Name = "navigation")]
public class NavigationItem
{
    [Column(IsPrimary = true, IsIdentity = true)] public long Id { get; set; }
    [Column(StringLength = 80)] public string Text { get; set; } = "";
    [Column(StringLength = 1000)] public string Url { get; set; } = "";
    public int SortOrder { get; set; }
    public long? ParentId { get; set; }
}
