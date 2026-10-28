using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WordWebCMS.Services;

public sealed class MathChallenge(IMemoryCache cache)
{
    private readonly object gate = new();
    public (string Token, string Question) Create(string browser)
    {
        var a = RandomNumberGenerator.GetInt32(1, 10);
        var b = RandomNumberGenerator.GetInt32(1, 10);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        cache.Set("math:" + token, (browser, a + b), TimeSpan.FromMinutes(10));
        return (token, $"{a} + {b} = ?");
    }

    public bool Verify(string browser, string token, string answer)
    {
        if (token.Length != 48 || browser.Length != 48) return false;
        lock (gate)
        {
            if (!cache.TryGetValue<(string Browser, int Answer)>("math:" + token, out var expected)) return false;
            cache.Remove("math:" + token);
            return expected.Browser == browser && int.TryParse(answer, out var number) && number == expected.Answer;
        }
    }
}

public class MathChallengeFilter(MathChallenge challenge) : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        if (context.ActionDescriptor.ViewEnginePath is not ("/Account/Login" or "/Account/Register" or "/Account/Recover" or "/Account/Reset"))
        { await next(); return; }
        var http = context.HttpContext;
        var browser = http.Request.Cookies["cms-challenge"] ?? "";
        if (HttpMethods.IsPost(http.Request.Method))
        {
            var form = await http.Request.ReadFormAsync();
            if (!challenge.Verify(browser, form["ChallengeToken"].ToString(), form["ChallengeAnswer"].ToString()))
                context.ModelState.AddModelError("", UiText.T("算术验证错误或已过期，请回答新的题目。"));
        }
        if (browser.Length != 48)
        {
            browser = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            http.Response.Cookies.Append("cms-challenge", browser, new CookieOptions {
                HttpOnly = true, Secure = http.Request.IsHttps, SameSite = SameSiteMode.Strict, IsEssential = true, Path = "/Account" });
        }
        var question = challenge.Create(browser);
        var model = (PageModel)context.HandlerInstance;
        model.ViewData["ChallengeToken"] = question.Token;
        model.ViewData["ChallengeQuestion"] = question.Question;
        await next();
    }
}
