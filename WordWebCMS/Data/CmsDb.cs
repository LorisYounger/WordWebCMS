using FreeSql;
using WordWebCMS.Models;
using WordWebCMS.Services;

namespace WordWebCMS.Data;

public sealed class CmsDb(IConfiguration config) : IDisposable
{
    private IFreeSql? accounts;
    private IFreeSql? site;
    public IFreeSql Accounts { get { if (accounts == null) Configure(config); return accounts!; } }
    public IFreeSql Site { get { if (site == null) Configure(config); return site!; } }
    public bool CheckSchema() => Accounts.DbFirst.ExistsTable("schema_version") && Site.DbFirst.ExistsTable("schema_version") &&
        Accounts.Select<SchemaVersion>().First()?.Version == 6 && Site.Select<SchemaVersion>().First()?.Version == 6;

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
    public void InitializeSchema(Action<string>? reportStep = null)
    {
        reportStep?.Invoke("检查站点数据库版本");
        var upgrading = Site.DbFirst.ExistsTable("schema_version") && Site.Select<SchemaVersion>().First()?.Version == 1;
        foreach (var (db, name) in new[] { (Accounts, "账号库"), (Site, "站点库") })
        {
            reportStep?.Invoke(name == "账号库" ? "检查账号数据库版本" : "检查站点数据库版本");
            var version = db.DbFirst.ExistsTable("schema_version") ? db.Select<SchemaVersion>().First()?.Version : null;
            if (version > 6)
                throw new InvalidOperationException(UiText.Format("{0}的版本为 {1}，当前程序只支持到版本 6。请部署更新的 WordWebCMS 程序。", UiText.T(name), version));
        }
        reportStep?.Invoke("更新账号数据表");
        Accounts.CodeFirst.SyncStructure(typeof(SchemaVersion), typeof(CmsUser), typeof(AccountToken));
        reportStep?.Invoke("更新站点数据表");
        Site.CodeFirst.SyncStructure(typeof(SchemaVersion), typeof(SiteUserRole), typeof(SiteSettings),
            typeof(Post), typeof(PostDraft), typeof(PostRevision), typeof(Category), typeof(Tag),
            typeof(RevisionTag), typeof(Comment), typeof(PostLike), typeof(CommentLike), typeof(Media), typeof(NavigationItem));
        reportStep?.Invoke("更新站点默认设置");
        Site.Update<SiteSettings>().Set(s => s.Language, "zh-Hans").Where(s => s.Language == null || s.Language == "" || upgrading).ExecuteAffrows();
        Site.Update<SiteSettings>().Set(s => s.EnabledLanguages, "zh-Hans,en").Where(s => s.EnabledLanguages == null || s.EnabledLanguages == "").ExecuteAffrows();
        reportStep?.Invoke("保存数据库版本");
        foreach (var db in new[] { Accounts, Site })
            db.InsertOrUpdate<SchemaVersion>().SetSource(new SchemaVersion { Version = 6 }).ExecuteAffrows();
    }

    public void Dispose()
    {
        accounts?.Dispose();
        if (!ReferenceEquals(accounts, site)) site?.Dispose();
    }

}
