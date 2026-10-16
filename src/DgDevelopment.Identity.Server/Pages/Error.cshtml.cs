namespace DgDevelopment.Identity.Server.Pages;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public sealed class ErrorModel : PageModel
{
    [FromQuery]
    public string? ErrorCode { get; set; }
    [FromQuery]
    public string? ErrorDescription { get; set; }

    public string ErrorMessage => ErrorDescription ?? ErrorCode ?? "An unknown error occurred.";

    // No server-side logic needed - the page just renders ErrorMessage from the bound query string.
    public static void OnGet() { }
}
