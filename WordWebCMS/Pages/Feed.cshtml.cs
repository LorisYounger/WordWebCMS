using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WordWebCMS.Data;
using WordWebCMS.Models;
using WordWebCMS.Services;

namespace WordWebCMS.Pages;
public class FeedModel(CmsDb db) : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        var settings = (SiteSettings)HttpContext.Items["SiteSettings"]!;
        var siteText = ContentLanguages.Display(settings);
        var rows = await db.Site.Select<Post, PostRevision>().InnerJoin((p, r) => p.PublishedRevisionId == r.Id)
            .Where((p, r) => p.IsPublic && p.Kind == PostKind.Article).OrderByDescending((p, r) => p.PublishedUtc).Limit(30)
            .ToListAsync((p, r) => new { p.Slug, p.PublishedUtc, Revision = r });
        var xml = new XDocument(new XElement("rss", new XAttribute("version", "2.0"),
            new XElement("channel", new XElement("title", siteText.Title), new XElement("link", settings.BaseUrl), new XElement("description", siteText.Description),
                rows.Select(r => {
                    var text = ContentLanguages.Display(r.Revision, settings);
                    var link = settings.BaseUrl + "/" + r.Slug + "?culture=" + UiText.Language;
                    return new XElement("item", new XElement("title", text.Title), new XElement("link", link),
                        new XElement("guid", link), new XElement("description", text.Summary),
                        new XElement("pubDate", DateTime.SpecifyKind(r.PublishedUtc!.Value, DateTimeKind.Utc).ToString("r")));
                }))));
        return HttpCache.Content(HttpContext, xml.ToString(), "application/rss+xml; charset=utf-8");
    }
}
