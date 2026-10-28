using Ganss.Xss;
using Markdig;

namespace WordWebCMS.Services;

public class MarkdownService
{
    private readonly MarkdownPipeline article = new MarkdownPipelineBuilder().UsePipeTables().UseAutoLinks().UseEmphasisExtras().Build();
    private readonly MarkdownPipeline comment = new MarkdownPipelineBuilder().UsePipeTables().DisableHtml().Build();
    private readonly HtmlSanitizer sanitizer = new();

    public MarkdownService()
    {
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.UnionWith("p br hr h1 h2 h3 h4 h5 h6 blockquote pre code strong em del s ul ol li a img table thead tbody tr th td".Split(' '));
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.UnionWith(new[] { "href", "src", "alt", "title", "colspan", "rowspan" });
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.UnionWith(new[] { "http", "https", "mailto" });
        sanitizer.AllowDataAttributes = false;
    }

    public string Render(string markdown, bool isComment = false) => sanitizer.Sanitize(Markdown.ToHtml(markdown, isComment ? comment : article));

    public static bool IsWebUrl(string value, bool allowMail = false) =>
        value.StartsWith('/') && !value.StartsWith("//") && !value.Contains('\\') && !value.Any(char.IsControl) ||
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == "https" || uri.Scheme == "http" || allowMail && uri.Scheme == "mailto");
}
