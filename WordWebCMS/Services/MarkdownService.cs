using Ganss.Xss;
using Markdig;
using AngleSharp.Html.Parser;
using System.Text.RegularExpressions;

namespace WordWebCMS.Services;

public class MarkdownService
{
    private readonly MarkdownPipeline article = new MarkdownPipelineBuilder().UsePipeTables().UseAutoLinks().UseEmphasisExtras().UseAutoIdentifiers().Build();
    private readonly MarkdownPipeline comment = new MarkdownPipelineBuilder().UsePipeTables().DisableHtml().Build();
    private readonly HtmlSanitizer sanitizer = new();

    public MarkdownService()
    {
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.UnionWith("p br hr h1 h2 h3 h4 h5 h6 blockquote pre code strong em del s ul ol li a img table thead tbody tr th td span div sup sub".Split(' '));
        sanitizer.KeepChildNodes = true;
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.UnionWith(new[] { "href", "src", "alt", "title", "colspan", "rowspan", "id", "style", "width", "height" });
        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedCssProperties.UnionWith(new[] { "color", "background-color", "text-align", "font-size", "font-weight", "font-style", "text-decoration" });
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.UnionWith(new[] { "http", "https", "mailto" });
        sanitizer.AllowDataAttributes = false;
    }

    public string Render(string markdown, bool isComment = false)
    {
        var html = sanitizer.Sanitize(Markdown.ToHtml(markdown, isComment ? comment : article));
        var document = new HtmlParser().ParseDocument(html);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in document.Body!.QuerySelectorAll("[id],h1,h2,h3,h4,h5,h6"))
        {
            var id = element.Id ?? "";
            if (!isComment && id == "" && Regex.IsMatch(element.LocalName, "^h[1-6]$"))
            {
                id = Regex.Replace(element.TextContent.ToLowerInvariant(), @"[^\p{L}\p{N}]+", "-").Trim('-');
                if (id.Length > 140) id = id[..140];
                if (id == "") id = "heading";
            }
            if (isComment || !Regex.IsMatch(element.LocalName, "^h[1-6]$") || !Regex.IsMatch(id, @"^[\p{L}\p{N}][\p{L}\p{N}_.:-]{0,159}$")) element.RemoveAttribute("id");
            else { var unique = id; for (var n = 2; !ids.Add(unique); n++) unique = id + "-" + n; element.Id = unique; }
        }
        foreach (var element in document.Body.QuerySelectorAll("[width],[height]"))
            foreach (var name in new[] { "width", "height" })
                if (element.HasAttribute(name) && (element.LocalName != "img" || !int.TryParse(element.GetAttribute(name), out var n) || n is < 1 or > 4096)) element.RemoveAttribute(name);
        return document.Body.InnerHtml;
    }

    public static bool IsWebUrl(string value, bool allowMail = false) =>
        value.StartsWith('/') && !value.StartsWith("//") && !value.Contains('\\') && !value.Any(char.IsControl) ||
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == "https" || uri.Scheme == "http" || allowMail && uri.Scheme == "mailto");
}
