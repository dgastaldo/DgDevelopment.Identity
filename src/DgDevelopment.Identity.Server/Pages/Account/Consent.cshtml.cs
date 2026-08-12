namespace DgDevelopment.Identity.Server.Pages.Account;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

internal sealed class ConsentModel : PageModel
{
    [FromQuery]
    public string? ReturnUrl { get; set; }

    public IActionResult OnPost(string action)
    {
        if (action == "approve" && !string.IsNullOrWhiteSpace(ReturnUrl))
            return LocalRedirect(ReturnUrl);

        return RedirectToPage("/Error");
    }
}
