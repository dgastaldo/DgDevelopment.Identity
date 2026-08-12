namespace DgDevelopment.Identity.Server.Pages;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

internal sealed class ErrorModel : PageModel
{
    [FromQuery]
    public string? ErrorCode { get; set; }
    [FromQuery]
    public string? ErrorDescription { get; set; }

    public string ErrorMessage => ErrorDescription ?? ErrorCode ?? "An unknown error occurred.";

    public static void OnGet() { }
}
