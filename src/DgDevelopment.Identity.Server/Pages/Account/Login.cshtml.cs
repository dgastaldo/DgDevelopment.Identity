namespace DgDevelopment.Identity.Server.Pages.Account;

using System.Globalization;
using System.Security.Claims;
using DgDevelopment.Identity.Application.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

[EnableRateLimiting("auth")]
public sealed class LoginModel : PageModel
{
    private readonly IUserAuthenticationService _authService;
    private readonly IServerSessionService _sessionService;

    public LoginModel(IUserAuthenticationService authService, IServerSessionService sessionService)
    {
        _authService = authService;
        _sessionService = sessionService;
    }

    [BindProperty] public string Identifier { get; set; } = string.Empty;
    public string? LoginHint { get; set; }

    public void OnGet([FromQuery] string? login_hint = null)
    {
        LoginHint = login_hint;
        Identifier = login_hint ?? Identifier;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(Identifier))
        {
            ModelState.AddModelError(string.Empty, "Enter your username or email.");
            return Page();
        }

        var user = await _authService.FindByIdentifierAsync(Identifier).ConfigureAwait(false);

        if (user != null)
        {
            var activeSession = await _sessionService.FindActiveAsync(user.Id).ConfigureAwait(false);
            if (activeSession != null)
            {
                await SignInAsync(user, activeSession.SessionId, rememberMe: false).ConfigureAwait(false);
                return SafeRedirect(returnUrl);
            }
        }

        var loginHint = user?.Username ?? Identifier;
        return RedirectToPage("/Account/Password", new { login_hint = loginHint, returnUrl });
    }

    private async Task SignInAsync(DgDevelopment.Identity.Domain.Entities.User user, string sessionId, bool rememberMe)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString(null, CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim("session_id", sessionId),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        var properties = new AuthenticationProperties { IsPersistent = rememberMe };
        if (rememberMe)
            properties.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), properties).ConfigureAwait(false);
    }

    private IActionResult SafeRedirect(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToPage("/Index");
    }
}