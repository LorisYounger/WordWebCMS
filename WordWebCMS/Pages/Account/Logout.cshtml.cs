using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WordWebCMS.Pages.Account;
public class LogoutModel : PageModel
{
    public async Task<IActionResult> OnPostAsync() { await HttpContext.SignOutAsync(); return LocalRedirect("/"); }
}
