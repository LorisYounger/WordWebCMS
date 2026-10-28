using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using WordWebCMS.Data;
using WordWebCMS.Models;
using WordWebCMS.Services;

// A deliberately site-specific migration. Unsupported relationships stay in the source
// snapshot and decision report instead of being guessed or silently discarded.
static class Migration
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    static string S(JsonElement row, string key) => row.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : "";
    static long N(JsonElement row, string key) => long.TryParse(S(row, key), out var n) ? n : 0;
    static DateTime D(JsonElement row, string key) => DateTime.Parse(S(row, key), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);
    static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }
    static void Save(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, Json));

    public static void Run(string operation, MigrationConfig config)
    {
        var target = new MySqlConnectionStringBuilder(config.TargetConnection);
        var source = new MySqlConnectionStringBuilder(config.SourceConnection);
        if (target.Database != "wwcms_lbnet" || source.Database != "lbnet" || target.Database == source.Database)
            throw new InvalidOperationException("This migration only supports lbnet -> wwcms_lbnet.");
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(config.OutputPath, "source-snapshot.json")));
        var root = document.RootElement;
        JsonElement[] Rows(string name) => root.GetProperty(name).EnumerateArray().ToArray();
        var allPosts = Rows("posts");
        var posts = allPosts.Where(p => S(p, "post_type") is "post" or "page" && S(p, "post_status") is "publish" or "draft" or "pending" or "private" or "future").ToArray();
        var revisions = allPosts.Where(p => S(p, "post_type") == "revision" && posts.Any(parent => N(parent, "ID") == N(p, "post_parent"))).ToArray();
        var meta = Rows("postmeta");
        string Meta(long id, string name) => meta.Where(m => N(m, "post_id") == id && S(m, "meta_key") == name).Select(m => S(m, "meta_value")).LastOrDefault() ?? "";
        var options = Rows("options").ToDictionary(o => S(o, "option_name"), o => S(o, "option_value"));
        var terms = Rows("terms").ToDictionary(t => N(t, "term_id"));
        var taxonomy = Rows("taxonomy");
        var relationships = Rows("relationships");
        JsonElement[] Taxa(long id, string type) => taxonomy.Where(t => S(t, "taxonomy") == type && relationships.Any(r => N(r, "object_id") == id && N(r, "term_taxonomy_id") == N(t, "term_taxonomy_id"))).ToArray();
        var categories = taxonomy.Where(t => S(t, "taxonomy") == "category").ToArray();
        var tags = taxonomy.Where(t => S(t, "taxonomy") == "post_tag").ToArray();
        var owner = Rows("users").Single(u => S(u, "user_login") == "zoujin");
        var ownerId = N(owner, "ID");
        if (posts.Any(p => N(p, "post_author") != ownerId || S(p, "post_password") != ""))
            throw new InvalidOperationException("Unreviewed authors or password-protected posts; do not publish implicitly.");
        if (Rows("comments").Length != 0) throw new InvalidOperationException("Approved comments require a reviewed user mapping before import.");
        if (posts.Any(p => !ShortLinks.IsValid(S(p, "post_name")))) throw new InvalidOperationException("Unmapped WordPress slugs.");
        var mediaRoot = Path.Combine(config.OutputPath, "data", "media");
        var mediaManifestPath = Path.Combine(config.OutputPath, "media-manifest.json");
        var uploads = Path.Combine(config.SourcePath, "wp-content", "uploads");
        var media = new List<MediaEntry>();
        // Only actual supported image assets, never PHP, plugin configuration, or ZIPs.
        foreach (var file in Directory.EnumerateFiles(uploads, "*.png", SearchOption.AllDirectories).Order())
        {
            var bytes = File.ReadAllBytes(file);
            if (bytes.Length < 12 || bytes.Length > 10 * 1024 * 1024 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                throw new InvalidDataException("Invalid or oversized PNG: " + Path.GetFileName(file));
            var relative = Path.GetRelativePath(uploads, file).Replace('\\', '/');
            var hash = Hash(file);
            media.Add(new(relative, "wp-" + hash[..24] + ".png", hash, bytes.Length));
        }
        var localUrls = media.ToDictionary(m => "/wp-content/uploads/" + m.Source, m => "/media/" + m.Target, StringComparer.OrdinalIgnoreCase);
        var hosts = new[] { new Uri(options["home"]).Host, new Uri(options["siteurl"]).Host }.ToHashSet(StringComparer.OrdinalIgnoreCase);
        string RewriteUrl(string text)
        {
            return Regex.Replace(text, @"https?://[^\s<>""\)]+", match => {
                if (!Uri.TryCreate(match.Value, UriKind.Absolute, out var url) || !hosts.Contains(url.Host)) return match.Value;
                if (localUrls.TryGetValue(Uri.UnescapeDataString(url.AbsolutePath), out var mediaUrl)) return mediaUrl + url.Query + url.Fragment;
                var slug = Uri.UnescapeDataString(url.AbsolutePath).Trim('/').ToLowerInvariant();
                if (posts.Any(p => S(p, "post_name") == slug)) return "/" + slug + url.Query + url.Fragment;
                return match.Value;
            });
        }
        string Body(JsonElement row)
        {
            var body = S(row, "post_content_filtered");
            if (body == "") body = S(row, "post_content");
            // Githuber source contains CR CR LF for a single logical newline.
            body = body.Replace("\r\r\n", "\n").Replace("\r\n", "\n").Replace('\r', '\n');
            return RewriteUrl(body);
        }
        var renderer = new MarkdownService();
        var parser = new HtmlParser();
        var manifest = posts.Select(p => new {
            SourceId = N(p, "ID"), Slug = S(p, "post_name"), Title = S(p, "post_title"), Status = S(p, "post_status"),
            SourceCategoryIds = Taxa(N(p, "ID"), "category").Select(t => N(t, "term_id")).ToArray(),
            CategoryDeferred = Taxa(N(p, "ID"), "category").Length > 1,
            SourceTagIds = Taxa(N(p, "ID"), "post_tag").Select(t => N(t, "term_id")).ToArray(),
            MarkdownSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Body(p)))).ToLowerInvariant(),
            RenderedTables = parser.ParseDocument(renderer.Render(Body(p))).QuerySelectorAll("table").Length
        }).ToArray();
        var credentialsPath = Path.Combine(config.OutputPath, "admin-credentials.private.json");
        if (operation == "prepare")
        {
            Directory.CreateDirectory(mediaRoot);
            foreach (var entry in media)
            {
                var dest = Path.Combine(mediaRoot, entry.Target);
                if (File.Exists(dest) && Hash(dest) != entry.Sha256) throw new InvalidDataException("Staged media differs.");
                if (!File.Exists(dest)) File.Copy(Path.Combine(uploads, entry.Source), dest);
            }
            Save(mediaManifestPath, media);
            Save(Path.Combine(config.OutputPath, "content-manifest.json"), manifest);
            Save(Path.Combine(config.OutputPath, "pending-category-relations.json"), new {
                Hierarchy = categories.Where(t => N(t, "parent") != 0).Select(t => new { SourceId = N(t, "term_id"), ParentId = N(t, "parent"), Name = S(terms[N(t, "term_id")], "name") }),
                Multiple = manifest.Where(p => p.CategoryDeferred)
            });
            Save(Path.Combine(config.OutputPath, "legacy-url-map.json"), meta.Where(m => S(m, "meta_key") == "_wp_old_slug" && posts.Any(p => N(p, "ID") == N(m, "post_id"))).Select(m => new { Source = "/" + S(m, "meta_value") + "/", Target = "/" + posts.Single(p => N(p, "ID") == N(m, "post_id")).GetProperty("post_name").GetString() }));
            if (!File.Exists(credentialsPath)) Save(credentialsPath, new { UserName = "zoujin", Password = "Ww!" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)), Note = "仅用于新站；旧 WordPress 密码保持不变。请登录后通过找回密码流程设置自己的密码。" });
            foreach (var post in posts)
            {
                var directory = Path.Combine(config.OutputPath, "rendered-preview");
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, S(post, "post_name") + ".html"), renderer.Render(Body(post)));
            }
            Console.WriteLine(JsonSerializer.Serialize(new { Posts = posts.Length, Public = posts.Count(p => S(p, "post_status") == "publish"), HistoricalRevisions = revisions.Length, Media = media.Count, Categories = categories.Length, Tags = tags.Length, DeferredMultipleCategories = manifest.Count(m => m.CategoryDeferred), TablesInSendkeys = manifest.Single(m => m.Slug == "sendkeys").RenderedTables }, Json));
            return;
        }
        foreach (var entry in media) if (Hash(Path.Combine(mediaRoot, entry.Target)) != entry.Sha256) throw new InvalidDataException("Staged asset hash mismatch.");
        using var db = new CmsDb(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:Accounts"] = config.TargetConnection, ["ConnectionStrings:Site"] = config.TargetConnection }).Build());
        var mapPath = Path.Combine(config.OutputPath, "import-map.json");
        if (operation == "theme")
        {
            if (!File.Exists(mapPath) || !File.Exists(Path.Combine(config.OutputPath, "data", "theme", "lbnet", "theme.json")))
                throw new InvalidOperationException("Prepare the imported site's theme first.");
            db.Site.Update<SiteSettings>().Set(s => s.Theme, "lbnet").Where(s => s.Id == 1 && s.Theme == "default").ExecuteAffrows();
            Console.WriteLine("Activated lbnet theme (default theme with original ICP footer and site icon).");
            return;
        }
        if (operation == "verify")
        {
            var map = JsonSerializer.Deserialize<ImportMap>(File.ReadAllText(mapPath))!;
            var errors = new List<string>();
            void Check(bool ok, string message) { if (!ok) errors.Add(message); }
            Check(db.CheckSchema(), "schema");
            Check(db.Site.Select<Post>().Count() == posts.Length, "post count");
            Check(db.Site.Select<PostRevision>().Count() == revisions.Length + posts.Length, "revision count");
            Check(db.Site.Select<PostDraft>().Count() == posts.Length, "draft count");
            Check(db.Site.Select<Category>().Count() == categories.Length, "category count");
            Check(db.Site.Select<Tag>().Count() == tags.Length, "tag count");
            Check(db.Site.Select<Media>().Count() == media.Count, "media count");
            Check(db.Site.Select<Comment>().Count() == 0, "comment filter");
            Check(db.Accounts.Select<CmsUser>().Count() == 1, "excluded second account");
            foreach (var p in posts)
            {
                var id = map.Posts[N(p, "ID")];
                var targetPost = db.Site.Select<Post>().Where(t => t.Id == id).First();
                var draft = db.Site.Select<PostDraft>().Where(t => t.PostId == id).First();
                var current = db.Site.Select<PostRevision>().Where(t => t.Id == map.CurrentRevisions[N(p, "ID")]).First();
                Check(draft.Markdown == Body(p) && current.Markdown == Body(p), "body " + id);
                Check(draft.Title == S(p, "post_title") && current.Title == S(p, "post_title"), "title " + id);
                Check(targetPost.IsPublic == (S(p, "post_status") == "publish"), "visibility " + id);
                Check(targetPost.Slug == S(p, "post_name"), "slug " + id);
                Check(targetPost.PublishedRevisionId == (targetPost.IsPublic ? current.Id : null), "published pointer " + id);
                if (targetPost.IsPublic) Check(targetPost.PublishedUtc == D(p, "post_date_gmt"), "published date " + id);
                var cats = Taxa(N(p, "ID"), "category");
                Check(draft.CategoryId == (cats.Length == 1 ? map.Categories[N(cats[0], "term_id")] : null), "category policy " + id);
                var expectedTags = Taxa(N(p, "ID"), "post_tag").Select(t => map.Tags[N(t, "term_id")]).Order().ToArray();
                Check(db.Site.Select<RevisionTag>().Where(t => t.RevisionId == current.Id).ToList(t => t.TagId).Order().SequenceEqual(expectedTags), "tags " + id);
            }
            foreach (var r in revisions) Check(db.Site.Select<PostRevision>().Where(t => t.Id == map.HistoricalRevisions[N(r, "ID")]).First().Markdown == Body(r), "historical body " + N(r, "ID"));
            foreach (var m in media) Check(db.Site.Select<Media>().Where(t => t.FileName == m.Target && t.Size == m.Size).Any(), "media metadata " + m.Target);
            using var credentials = JsonDocument.Parse(File.ReadAllText(credentialsPath));
            var account = db.Accounts.Select<CmsUser>().Where(u => u.UserName == "zoujin").First();
            Check(new PasswordHasher<CmsUser>().VerifyHashedPassword(account, account.PasswordHash, credentials.RootElement.GetProperty("Password").GetString()!) != PasswordVerificationResult.Failed, "admin password");
            Check(db.Site.Select<NavigationItem>().Count() == 5, "navigation count");
            var result = new { Passed = errors.Count == 0, Errors = errors, CheckedUtc = DateTime.UtcNow, Posts = posts.Length, Revisions = revisions.Length + posts.Length, Media = media.Count };
            Save(Path.Combine(config.OutputPath, "verification.json"), result);
            Console.WriteLine(JsonSerializer.Serialize(result, Json));
            if (errors.Count != 0) throw new InvalidDataException("Migration verification failed.");
            return;
        }
        if (operation != "import") throw new ArgumentException("Unknown operation.");
        using (var connection = new MySqlConnection(new MySqlConnectionStringBuilder(target.ConnectionString) { Database = "" }.ConnectionString))
        {
            connection.Open();
            using var command = new MySqlCommand("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='wwcms_lbnet'", connection);
            if (Convert.ToInt64(command.ExecuteScalar()) != 0) throw new InvalidOperationException("Target must be empty; refusing overwrite or duplicate import.");
            command.CommandText = "CREATE DATABASE IF NOT EXISTS `wwcms_lbnet` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
            command.ExecuteNonQuery();
        }
        // DDL is outside the transaction; all imported account/site rows share one transaction.
        db.InitializeSchema();
        var mapping = new ImportMap();
        using var secret = JsonDocument.Parse(File.ReadAllText(credentialsPath));
        db.Site.Transaction(() => {
            var user = new CmsUser { UserName = "zoujin", Email = S(owner, "user_email").ToLowerInvariant(), EmailConfirmed = true, CreatedUtc = D(owner, "user_registered") };
            user.PasswordHash = new PasswordHasher<CmsUser>().HashPassword(user, secret.RootElement.GetProperty("Password").GetString()!);
            user.Id = db.Accounts.Insert(user).ExecuteIdentity();
            db.Site.Insert(new SiteUserRole { UserId = user.Id, Role = SiteRole.Administrator }).ExecuteAffrows();
            foreach (var c in categories) mapping.Categories[N(c, "term_id")] = db.Site.Insert(new Category { Name = S(terms[N(c, "term_id")], "name") }).ExecuteIdentity();
            foreach (var t in tags) mapping.Tags[N(t, "term_id")] = db.Site.Insert(new Tag { Name = S(terms[N(t, "term_id")], "name") }).ExecuteIdentity();
            foreach (var m in media)
            {
                var attachment = allPosts.FirstOrDefault(p => S(p, "post_type") == "attachment" && Meta(N(p, "ID"), "_wp_attached_file") == m.Source);
                var attachmentId = attachment.ValueKind == JsonValueKind.Undefined ? 0 : N(attachment, "ID");
                mapping.Media[m.Source] = db.Site.Insert(new Media { FileName = m.Target, OriginalName = Path.GetFileName(m.Source), ContentType = "image/png", Size = m.Size,
                    UserId = user.Id, AltText = attachmentId == 0 ? "" : Meta(attachmentId, "_wp_attachment_image_alt"),
                    CreatedUtc = attachmentId == 0 ? D(root, "capturedUtc") : D(attachment, "post_date_gmt") }).ExecuteIdentity();
            }
            foreach (var p in posts)
            {
                var id = N(p, "ID");
                var post = new Post { AuthorId = user.Id, Kind = S(p, "post_type") == "page" ? PostKind.Page : PostKind.Article,
                    Slug = S(p, "post_name"), CreatedUtc = D(p, "post_date_gmt"), IsPublic = S(p, "post_status") == "publish",
                    PublishedUtc = S(p, "post_status") == "publish" ? D(p, "post_date_gmt") : null };
                post.Id = db.Site.Insert(post).ExecuteIdentity();
                mapping.Posts[id] = post.Id;
                var categoryList = Taxa(id, "category");
                var tagList = Taxa(id, "post_tag");
                long? category = categoryList.Length == 1 ? mapping.Categories[N(categoryList[0], "term_id")] : null;
                string tagText = string.Join(", ", tagList.Select(t => S(terms[N(t, "term_id")], "name")));
                PostRevision MakeRevision(JsonElement content, bool current) => new() {
                    PostId = post.Id, EditorId = user.Id, Title = S(content, "post_title"), Slug = post.Slug, Summary = S(content, "post_excerpt"), Markdown = Body(content),
                    CategoryId = current ? category : null, Tags = current ? tagText : "", AllowComments = post.Kind == PostKind.Article && S(p, "comment_status") == "open",
                    CreatedUtc = D(content, "post_modified_gmt"), State = current && post.IsPublic ? RevisionState.Published : RevisionState.Saved };
                foreach (var old in revisions.Where(r => N(r, "post_parent") == id).OrderBy(r => D(r, "post_modified_gmt")))
                    mapping.HistoricalRevisions[N(old, "ID")] = db.Site.Insert(MakeRevision(old, false)).ExecuteIdentity();
                var revision = MakeRevision(p, true);
                revision.Id = db.Site.Insert(revision).ExecuteIdentity();
                mapping.CurrentRevisions[id] = revision.Id;
                foreach (var t in tagList) db.Site.Insert(new RevisionTag { RevisionId = revision.Id, TagId = mapping.Tags[N(t, "term_id")] }).ExecuteAffrows();
                if (post.IsPublic) { post.PublishedRevisionId = revision.Id; db.Site.Update<Post>().SetSource(post).ExecuteAffrows(); }
                db.Site.Insert(new PostDraft { PostId = post.Id, Title = revision.Title, Slug = revision.Slug, Summary = revision.Summary, Markdown = revision.Markdown,
                    CategoryId = category, Tags = tagText, AllowComments = revision.AllowComments, UpdatedUtc = D(p, "post_modified_gmt") }).ExecuteAffrows();
            }
            foreach (var nav in allPosts.Where(p => S(p, "post_type") == "nav_menu_item" && S(p, "post_status") == "publish").OrderBy(p => N(p, "menu_order")))
            {
                var id = N(nav, "ID");
                var objectId = long.Parse(Meta(id, "_menu_item_object_id"));
                var type = Meta(id, "_menu_item_type");
                string url, title = S(nav, "post_title");
                if (Meta(id, "_menu_item_menu_item_parent") != "0") throw new InvalidDataException("Nested menu needs review.");
                if (type == "taxonomy" && Meta(id, "_menu_item_object") == "category")
                { url = PublicUrls.Category(S(terms[objectId], "name")); if (title == "") title = S(terms[objectId], "name"); }
                else if (type == "post_type")
                { var p = posts.Single(p => N(p, "ID") == objectId); url = "/" + S(p, "post_name"); if (title == "") title = S(p, "post_title"); }
                else if (type == "custom" && Meta(id, "_menu_item_url").TrimEnd('/') == options["home"].TrimEnd('/')) url = "/";
                else throw new InvalidDataException("Unreviewed menu link.");
                db.Site.Insert(new NavigationItem { Text = title, Url = url, SortOrder = (int)N(nav, "menu_order") }).ExecuteAffrows();
            }
            var icon = media.Single(m => m.Source == Meta(long.Parse(options["site_icon"]), "_wp_attached_file"));
            db.Site.Insert(new SiteSettings { Title = options["blogname"], Description = options["blogdescription"], BaseUrl = config.BaseUrl, Theme = "default",
                CommentsEnabled = options["default_comment_status"] == "open", ModerateComments = true, Registration = RegistrationMode.Closed,
                AvatarUserName = user.UserName, AvatarUrl = "/media/" + icon.Target, Language = "zh-Hans", EnabledLanguages = "zh-Hans,en" }).ExecuteAffrows();
        });
        Save(mapPath, mapping);
        Console.WriteLine("Committed migration to wwcms_lbnet; source untouched. Run verify next.");
    }

    public record MediaEntry(string Source, string Target, string Sha256, long Size);
    public class ImportMap
    {
        public Dictionary<long, long> Posts { get; set; } = [];
        public Dictionary<long, long> CurrentRevisions { get; set; } = [];
        public Dictionary<long, long> HistoricalRevisions { get; set; } = [];
        public Dictionary<long, long> Categories { get; set; } = [];
        public Dictionary<long, long> Tags { get; set; } = [];
        public Dictionary<string, long> Media { get; set; } = [];
    }
}
