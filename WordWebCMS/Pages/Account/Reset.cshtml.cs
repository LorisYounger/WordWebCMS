using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using WordWebCMS.Services;

namespace WordWebCMS.Pages.Account;

public class ResetModel(AccountService accounts) : PageModel
{
    [BindProperty(SupportsGet = true), Required, StringLength(64)] public string Token { get; set; } = "";
    [BindProperty, Required, StringLength(128, MinimumLength = 12)] public string Password { get; set; } = "";
    public bool Done { get; set; }
    public IActionResult OnPost()
    {
        if (!ModelState.IsValid) return Page();
        accounts.ConsumeToken(Token, "reset", Password);
        Done = true;
        return Page();
    }
}
