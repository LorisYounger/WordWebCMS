using System.Text.Json;
using MySqlConnector;

if (args.Length != 2) throw new ArgumentException("Usage: WordPressMigration <preflight|inspect|snapshot|prepare|import|verify|theme|mail-inspect|migrate-mail|smoke> <private-config.json>");
var config = JsonSerializer.Deserialize<MigrationConfig>(File.ReadAllText(args[1]))!;
if (args[0] == "smoke") { await Smoke.Run(config); return; }
if (args[0] == "migrate-mail") { await MailMigration.Run(config); return; }
if (args[0] is "prepare" or "import" or "verify" or "theme") { Migration.Run(args[0], config); return; }
var sourceBuilder = new MySqlConnectionStringBuilder(config.SourceConnection) { AllowZeroDateTime = true, ConvertZeroDateTime = true };
if (sourceBuilder.Database == new MySqlConnectionStringBuilder(config.TargetConnection).Database)
    throw new InvalidOperationException("Source and target databases must be different.");
if (args[0] == "preflight")
{
    var target = new MySqlConnectionStringBuilder(config.TargetConnection) { Database = "" };
    using var server = new MySqlConnection(target.ConnectionString);
    server.Open();
    foreach (var sql in new[] { "SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME IN ('lbnet','wwcms_lbnet')", "SHOW GRANTS FOR CURRENT_USER" })
    {
        using var command = new MySqlCommand(sql, server);
        using var reader = command.ExecuteReader();
        while (reader.Read()) Console.WriteLine(System.Text.RegularExpressions.Regex.Replace(reader.GetString(0), "IDENTIFIED BY PASSWORD '[^']*'", "IDENTIFIED BY PASSWORD '[redacted]'"));
    }
    return;
}
using var source = new MySqlConnection(sourceBuilder.ConnectionString);
source.Open();
using (var readOnly = new MySqlCommand("SET SESSION TRANSACTION READ ONLY", source)) readOnly.ExecuteNonQuery();
var jsonOptions = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
List<Dictionary<string, object?>> Query(string sql)
{
    using var command = new MySqlCommand(sql, source);
    using var reader = command.ExecuteReader();
    var result = new List<Dictionary<string, object?>>();
    while (reader.Read())
    {
        var row = new Dictionary<string, object?>();
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
            row[reader.GetName(i)] = value is MySqlDateTime date ? date.IsValidDateTime ? date.GetDateTime() : null : value;
        }
        result.Add(row);
    }
    return result;
}
if (args[0] == "snapshot")
{
    using (var begin = new MySqlCommand("START TRANSACTION WITH CONSISTENT SNAPSHOT, READ ONLY", source)) begin.ExecuteNonQuery();
    var snapshot = new Dictionary<string, object?> {
        ["capturedUtc"] = DateTime.UtcNow,
        ["posts"] = Query("SELECT * FROM wp_posts ORDER BY ID"),
        ["postmeta"] = Query("SELECT * FROM wp_postmeta ORDER BY meta_id"),
        ["terms"] = Query("SELECT * FROM wp_terms ORDER BY term_id"),
        ["taxonomy"] = Query("SELECT * FROM wp_term_taxonomy ORDER BY term_taxonomy_id"),
        ["relationships"] = Query("SELECT * FROM wp_term_relationships ORDER BY object_id,term_taxonomy_id"),
        ["users"] = Query("SELECT ID,user_login,user_email,user_registered,display_name,user_status FROM wp_users ORDER BY ID"),
        ["usermeta"] = Query("SELECT user_id,meta_key,meta_value FROM wp_usermeta WHERE meta_key IN ('wp_capabilities','nickname','description','first_name','last_name') ORDER BY umeta_id"),
        ["comments"] = Query("SELECT * FROM wp_comments WHERE comment_approved='1' ORDER BY comment_ID"),
        ["commentCounts"] = Query("SELECT comment_approved,comment_type,COUNT(*) AS count FROM wp_comments GROUP BY comment_approved,comment_type"),
        ["options"] = Query("SELECT option_name,option_value FROM wp_options WHERE option_name IN ('blogname','blogdescription','home','siteurl','permalink_structure','template','stylesheet','active_plugins','show_on_front','page_on_front','page_for_posts','timezone_string','gmt_offset','users_can_register','default_comment_status','comment_moderation','sticky_posts','WPLANG','site_icon','theme_mods_pacify','theme_mods_mok','sidebars_widgets','widget_text','widget_custom_html','widget_block','widget_nav_menu') ORDER BY option_name"),
        ["optionNames"] = Query("SELECT option_name,LENGTH(option_value) AS bytes FROM wp_options ORDER BY option_name")
    };
    var path = Path.Combine(config.OutputPath, "source-snapshot.json");
    if (File.Exists(path)) throw new InvalidOperationException("Snapshot already exists; preserve it and use a new output directory.");
    Directory.CreateDirectory(config.OutputPath);
    File.WriteAllText(path, JsonSerializer.Serialize(snapshot, jsonOptions));
    Console.WriteLine("Saved read-only source snapshot (approved comments only; no password hashes): " + path);
    return;
}
if (args[0] == "mail-inspect")
{
    var raw = Query("SELECT option_value FROM wp_options WHERE option_name='wp_mail_smtp'").Single()["option_value"]!.ToString()!;
    string Field(string key) => System.Text.RegularExpressions.Regex.Match(raw, "s:" + key.Length + ":\"" + key + "\";s:[0-9]+:\"(.*?)\";", System.Text.RegularExpressions.RegexOptions.Singleline).Groups[1].Value;
    var profile = new { Mailer = Field("mailer"), Encryption = Field("encryption"), HasHost = Field("host").Length > 0, HasPassword = Field("pass").Length > 0,
        PasswordFormat = "plugin-managed; migrate-mail verifies the secretbox MAC", HasUser = Field("user").Length > 0 };
    File.WriteAllText(Path.Combine(config.OutputPath, "mail-profile.json"), JsonSerializer.Serialize(profile, jsonOptions));
    Console.WriteLine(JsonSerializer.Serialize(profile, jsonOptions));
    return;
}
if (args[0] == "inspect")
{
    var tables = Query("SELECT TABLE_NAME, TABLE_ROWS, ENGINE FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() ORDER BY TABLE_NAME");
    var prefixes = tables.Select(t => t["TABLE_NAME"]!.ToString()!).Where(t => t.EndsWith("_posts")).Select(t => t[..^5]).ToArray();
    if (prefixes.Length != 1) throw new InvalidOperationException("Expected one WordPress table prefix.");
    var prefix = prefixes[0];
    if (!System.Text.RegularExpressions.Regex.IsMatch(prefix, "^[a-zA-Z0-9_]+$")) throw new InvalidOperationException("Invalid prefix");
    var report = new Dictionary<string, object?> {
        ["prefix"] = prefix, ["tables"] = tables,
        ["posts"] = Query($"SELECT post_type,post_status,COUNT(*) AS count FROM `{prefix}posts` GROUP BY post_type,post_status"),
        ["comments"] = Query($"SELECT comment_type,comment_approved,COUNT(*) AS count,SUM(user_id=0) AS guests FROM `{prefix}comments` GROUP BY comment_type,comment_approved"),
        ["taxonomy"] = Query($"SELECT taxonomy,COUNT(*) AS count,SUM(parent<>0) AS children FROM `{prefix}term_taxonomy` GROUP BY taxonomy"),
        ["options"] = Query($"SELECT option_name,option_value FROM `{prefix}options` WHERE option_name IN ('blogname','blogdescription','home','siteurl','permalink_structure','template','stylesheet','active_plugins','show_on_front','page_on_front','page_for_posts','timezone_string','gmt_offset','users_can_register','default_comment_status','comment_moderation','sticky_posts','WPLANG')"),
        ["users"] = Query($"SELECT ID,user_login,display_name,user_registered FROM `{prefix}users`"),
        ["roles"] = Query($"SELECT user_id,meta_key,meta_value FROM `{prefix}usermeta` WHERE meta_key='{prefix}capabilities'"),
        ["postMeta"] = Query($"SELECT meta_key,COUNT(*) AS count FROM `{prefix}postmeta` GROUP BY meta_key ORDER BY count DESC"),
        ["contentInventory"] = Query($"SELECT ID,post_author,post_type,post_status,post_title,post_name,post_parent,CHAR_LENGTH(post_content) AS length,CHAR_LENGTH(post_excerpt) AS excerpt_length,post_date_gmt,comment_status,(post_password<>'') AS has_password FROM `{prefix}posts` WHERE post_type<>'revision' ORDER BY ID"),
        ["approvedComments"] = Query($"SELECT comment_ID,comment_post_ID,comment_parent,user_id,comment_type,CHAR_LENGTH(comment_content) AS length FROM `{prefix}comments` WHERE comment_approved='1'"),
        ["targetTables"] = Query("SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA='wwcms_lbnet'")
    };
    Directory.CreateDirectory(config.OutputPath);
    File.WriteAllText(Path.Combine(config.OutputPath, "inspection.json"), JsonSerializer.Serialize(report, jsonOptions));
    Console.WriteLine(JsonSerializer.Serialize(report, jsonOptions));
    return;
}
throw new ArgumentException("Unknown operation");

record MigrationConfig(string SourceConnection, string TargetConnection, string SourcePath, string OutputPath,
    string BaseUrl = "http://127.0.0.1:5197", string DeploymentPath = "");
