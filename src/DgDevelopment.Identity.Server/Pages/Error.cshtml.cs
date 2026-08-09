namespace DgDevelopment.Identity.Server.Pages;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public sealed class ErrorModel : PageModel
{
    [FromQuery]
    public string? Error { get; set; }
    [FromQuery]
    public string? ErrorDescription { get; set; }

    public string ErrorMessage => ErrorDescription ?? Error ?? "An unknown error occurred.";

    public void OnGet() { }
}
