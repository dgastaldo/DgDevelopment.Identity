namespace DgDevelopment.Identity.Server.Pages.Account;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public sealed class CheckEmailModel : PageModel
{
    [FromQuery] public string? Purpose { get; set; }

    public void OnGet()
    {
        // No server-side logic needed - the page just renders Purpose-dependent copy.
    }
}
