using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace WordWebCMS.Services;

public static class HttpCache
{
    public static IActionResult Content(HttpContext http, string html, string contentType, int status = 200)
    {
        // Cookie also distinguishes a cached visitor page after login/logout or a language change.
        http.Response.Headers.AppendCommaSeparatedValues("Vary", "Cookie", "Accept-Language");
        var tempDataCookie = http.RequestServices.GetRequiredService<IOptions<CookieTempDataProviderOptions>>().Value.Cookie.Name!;
        var cacheable = status == 200 && (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method)) &&
            http.User.Identity?.IsAuthenticated != true && !http.Request.Headers.ContainsKey("Authorization") &&
            !http.Request.Cookies.ContainsKey(tempDataCookie) && !http.Response.Headers.ContainsKey("Set-Cookie") &&
            http.Response.GetTypedHeaders().CacheControl?.NoStore != true;
        if (cacheable)
        {
            http.Response.Headers.CacheControl = "public, no-cache";
            http.Response.Headers.Remove("Pragma");
            var etag = new EntityTagHeaderValue($"\"{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(html)))}\"", isWeak: true);
            http.Response.GetTypedHeaders().ETag = etag;
            if (EntityTagHeaderValue.TryParseList(http.Request.Headers.IfNoneMatch, out var requested) &&
                requested.Any(value => value.Equals(EntityTagHeaderValue.Any) || value.Compare(etag, useStrongComparison: false)))
                return new StatusCodeResult(StatusCodes.Status304NotModified);
        }
        else
        {
            http.Response.Headers.CacheControl = "no-cache, no-store";
            http.Response.Headers.Pragma = "no-cache";
        }
        return new ContentResult { Content = html, ContentType = contentType, StatusCode = status };
    }
}
