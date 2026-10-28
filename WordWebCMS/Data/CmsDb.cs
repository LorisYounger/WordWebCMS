using FreeSql;
using WordWebCMS.Models;

namespace WordWebCMS.Data;

public sealed class CmsDb(IConfiguration config) : IDisposable
{
    private IFreeSql? accounts;
    private IFreeSql? site;
    public IFreeSql Accounts { get { if (accounts == null) Configure(config); return accounts!; } }
    public IFreeSql Site { get { if (site == null) Configure(config); return site!; } }
    public bool CheckSchema() => Accounts.DbFirst.ExistsTable("schema_version") && Site.DbFirst.ExistsTable("schema_version") &&
        Accounts.Select<SchemaVersion>().First()?.Version == 4 && Site.Select<SchemaVersion>().First()?.Version == 4;

    // Reconfigure only during startup or under the setup lock while normal traffic is unavailable.
    public void Configure(IConfiguration config)
    {
        var accounts = new FreeSqlBuilder().UseConnectionString(DataType.MySql,
            config.GetConnectionString("Accounts")).UseAutoSyncStructure(false).Build();
        IFreeSql site;
        try
        {
            site = config.GetConnectionString("Accounts") == config.GetConnectionString("Site") ? accounts :
                new FreeSqlBuilder().UseConnectionString(DataType.MySql,
                config.GetConnectionString("Site")).UseAutoSyncStructure(false).Build();
        }
        catch { accounts.Dispose(); throw; }
        Dispose();
        this.accounts = accounts;
        this.site = site;
    }

    // Startup migration and the token-protected setup action share this migration path.
    public void InitializeSchema()
    {
        var upgrading = Site.DbFirst.ExistsTable("schema_version") && Site.Select<SchemaVersion>().First()?.Version == 1;
        foreach (var db in new[] { Accounts, Site })
        {
            var tables = db.DbFirst.GetTablesByDatabase();
            if (tables.Count > 0 && !tables.Any(t => t.Name == "schema_version"))
                throw new InvalidOperationException("请使用空的新数据库；不支持覆盖旧版数据表。");
            if (db.DbFirst.ExistsTable("schema_version") &&
                db.Select<SchemaVersion>().First()?.Version > 4)
                throw new InvalidOperationException("数据库版本比程序新，请使用对应的程序版本。");
        }
        Accounts.CodeFirst.SyncStructure(typeof(SchemaVersion), typeof(CmsUser), typeof(AccountToken));
        Site.CodeFirst.SyncStructure(typeof(SchemaVersion), typeof(SiteUserRole), typeof(SiteSettings),
            typeof(Post), typeof(PostDraft), typeof(PostRevision), typeof(Category), typeof(Tag),
            typeof(RevisionTag), typeof(Comment), typeof(PostLike), typeof(CommentLike), typeof(Media), typeof(NavigationItem));
        Site.Update<SiteSettings>().Set(s => s.Language, "zh-Hans").Where(s => s.Language == null || s.Language == "" || upgrading).ExecuteAffrows();
        Site.Update<SiteSettings>().Set(s => s.EnabledLanguages, "zh-Hans,en").Where(s => s.EnabledLanguages == null || s.EnabledLanguages == "").ExecuteAffrows();
        var administrator = Site.Select<SiteUserRole>().Where(r => r.Role == SiteRole.Administrator).OrderBy(r => r.UserId).First();
        var avatarName = administrator == null ? "admin" : Accounts.Select<CmsUser>().Where(u => u.Id == administrator.UserId).First()?.UserName ?? "admin";
        Site.Update<SiteSettings>().Set(s => s.AvatarUserName, avatarName).Where(s => s.AvatarUserName == null || s.AvatarUserName == "" || upgrading).ExecuteAffrows();
        foreach (var db in new[] { Accounts, Site })
            db.InsertOrUpdate<SchemaVersion>().SetSource(new SchemaVersion { Version = 4 }).ExecuteAffrows();
    }

    public void Dispose()
    {
        accounts?.Dispose();
        if (!ReferenceEquals(accounts, site)) site?.Dispose();
    }

}
