using FreeSql.DataAnnotations;

namespace WordWebCMS.Models;

[Table(Name = "cms_user")]
[Index("ux_user_name", nameof(UserName), true)]
[Index("ux_user_email", nameof(Email), true)]
public class CmsUser
{
    [Column(IsPrimary = true, IsIdentity = true)] public long Id { get; set; }
    [Column(StringLength = 64)] public string UserName { get; set; } = "";
    [Column(StringLength = 254)] public string Email { get; set; } = "";
    [Column(StringLength = 512)] public string PasswordHash { get; set; } = "";
    [Column(StringLength = 1000)] public string Bio { get; set; } = "";
    [Column(StringLength = 1000)] public string AvatarUrl { get; set; } = "";
    public bool EmailConfirmed { get; set; }
    public bool Disabled { get; set; }
    [Column(StringLength = 32)] public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

[Table(Name = "account_token")]
[Index("ix_token_user", nameof(UserId) + "," + nameof(Purpose), false)]
public class AccountToken
{
    [Column(IsPrimary = true, StringLength = 64)] public string Hash { get; set; } = "";
    public long UserId { get; set; }
    [Column(StringLength = 16)] public string Purpose { get; set; } = "";
    public DateTime ExpiresUtc { get; set; }
}

public enum SiteRole { Removed = -3, Pending = -2, Banned = -1, Reader, Contributor, Editor, Administrator }

[Table(Name = "site_user_role")]
public class SiteUserRole
{
    [Column(IsPrimary = true)] public long UserId { get; set; }
    [Column(MapType = typeof(int))] public SiteRole Role { get; set; }
}

[Table(Name = "schema_version")]
public class SchemaVersion
{
    [Column(IsPrimary = true)] public int Id { get; set; } = 1;
    public int Version { get; set; }
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
