using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WordWebCMS.Services;
using WordWebCMS.Data;
using WordWebCMS.Models;
using Microsoft.AspNetCore.WebUtilities;

namespace WordWebCMS.Pages;

public class LanguageModel(CmsDb db) : PageModel
{
    public async Task<IActionResult> OnGetAsync(string culture, string? returnUrl)
    {
        culture = UiText.Normalize(culture ?? "");
        if (!UiText.Enabled(await db.Site.Select<SiteSettings>().FirstAsync()).Any(c => c.Name == culture)) return BadRequest();
        Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Lax, IsEssential = true });
        var destination = Url.IsLocalUrl(returnUrl) ? returnUrl! : "/";
        var hash = destination.IndexOf('#');
        var fragment = hash >= 0 ? destination[hash..] : "";
        if (hash >= 0) destination = destination[..hash];
        var question = destination.IndexOf('?');
        if (question >= 0)
        {
            var query = QueryHelpers.ParseQuery(destination[question..]);
            query.Remove("culture"); query.Remove("ui-culture");
            destination = QueryHelpers.AddQueryString(destination[..question], query);
        }
        return LocalRedirect(destination + fragment);
    }
}
