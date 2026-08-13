namespace DgDevelopment.Identity.Server.Pages.Account;

using System.Globalization;
using DgDevelopment.Identity.Application.Services;
using DgDevelopment.Identity.Domain.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

public sealed class LoginModel(IUserAuthenticationService authService, IUserSessionRepository sessionRepo) : PageModel
{
    [BindProperty] public string Username { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ModelState.AddModelError(string.Empty, "Username and password are required.");
            return Page();
        }

        var user = await authService.ValidateCredentialsAsync(Username, Password).ConfigureAwait(false);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return Page();
        }

        await authService.RecordSuccessfulLoginAsync(user).ConfigureAwait(false);

        var sessionId = Guid.NewGuid().ToString("N");
        var session = new DgDevelopment.Identity.Domain.Entities.UserSession(user.Id, sessionId, DateTime.UtcNow.AddHours(8), ["pwd"]);
        await sessionRepo.AddAsync(session).ConfigureAwait(false);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString(null, CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.Username),
            new("session_id", sessionId),
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(returnUrl)) return LocalRedirect(returnUrl);
        return RedirectToPage("/Index");
    }
}
