namespace DgDevelopment.Identity.Server.Pages.Account;

using DgDevelopment.Identity.Application.Services;
using DgDevelopment.Identity.Domain.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

public sealed class LoginModel : PageModel
{
    private readonly IUserAuthenticationService _authService;
    private readonly IUserSessionRepository _sessionRepo;

    public LoginModel(IUserAuthenticationService authService, IUserSessionRepository sessionRepo)
    {
        _authService = authService;
        _sessionRepo = sessionRepo;
    }

    [BindProperty] public string Username { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ModelState.AddModelError(string.Empty, "Username and password are required.");
            return Page();
        }

        var user = await _authService.ValidateCredentialsAsync(Username, Password).ConfigureAwait(false);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return Page();
        }

        await _authService.RecordSuccessfulLoginAsync(user).ConfigureAwait(false);

        var sessionId = Guid.NewGuid().ToString("N");
        var session = new DgDevelopment.Identity.Domain.Entities.UserSession(user.Id, sessionId, DateTime.UtcNow.AddHours(8), ["pwd"]);
        await _sessionRepo.AddAsync(session).ConfigureAwait(false);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
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
