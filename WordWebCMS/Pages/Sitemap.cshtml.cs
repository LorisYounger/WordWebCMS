using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WordWebCMS.Data;
using WordWebCMS.Models;

namespace WordWebCMS.Pages;
public class SitemapModel(CmsDb db) : PageModel
{
    public async Task<IActionResult> OnGetAsync(int? p)
    {
        if (p is < 1 or > 50000) return BadRequest();
        var settings = await db.Site.Select<SiteSettings>().FirstAsync();
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var query = db.Site.Select<Post, PostRevision>().InnerJoin((post, r) => post.PublishedRevisionId == r.Id).Where((post, r) => post.IsPublic);
        var total = await query.CountAsync();
        if (!p.HasValue && total > 1000)
            return Content(new XDocument(new XElement(ns + "sitemapindex", Enumerable.Range(1, (int)Math.Min(50000, (total + 999) / 1000))
                .Select(i => new XElement(ns + "sitemap", new XElement(ns + "loc", settings.BaseUrl + "/sitemap.xml?p=" + i))))).ToString(), "application/xml; charset=utf-8");
        var rows = await query.OrderBy((post, r) => post.Id).Page(p ?? 1, 1000).ToListAsync((post, r) => new { post.Slug, r.CreatedUtc });
        return Content(new XDocument(new XElement(ns + "urlset", new XElement(ns + "url", new XElement(ns + "loc", settings.BaseUrl + "/")),
            rows.Select(r => new XElement(ns + "url", new XElement(ns + "loc", settings.BaseUrl + "/" + r.Slug),
                new XElement(ns + "lastmod", r.CreatedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ")))))).ToString(), "application/xml; charset=utf-8");
    }
}
