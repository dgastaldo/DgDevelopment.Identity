namespace DgDevelopment.Identity.Server.Pages.Account;

using System.Globalization;
using System.Security.Claims;
using DgDevelopment.Identity.Application.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public sealed class PasswordModel : PageModel
{
    private readonly IUserAuthenticationService _authService;
    private readonly IServerSessionService _sessionService;
    private readonly IMfaPolicyService _mfaPolicy;

    public PasswordModel(
        IUserAuthenticationService authService,
        IServerSessionService sessionService,
        IMfaPolicyService mfaPolicy)
    {
        _authService = authService;
        _sessionService = sessionService;
        _mfaPolicy = mfaPolicy;
    }

    [BindProperty] public string Username { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;
    [BindProperty] public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }

    public IActionResult OnGet([FromQuery] string? login_hint = null, [FromQuery] string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(login_hint))
            return RedirectToPage("/Account/Login");

        Username = login_hint;
        ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ModelState.AddModelError(string.Empty, "Enter your password.");
            return Page();
        }

        var user = await _authService.ValidateCredentialsAsync(Username, Password).ConfigureAwait(false);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return Page();
        }

        await _authService.RecordSuccessfulLoginAsync(user).ConfigureAwait(false);

        if (await _mfaPolicy.RequiresMfaStepAsync(user).ConfigureAwait(false))
        {
            await SignInPartialAsync(user, RememberMe).ConfigureAwait(false);
            return RedirectToPage("/Account/Mfa", new { returnUrl });
        }

        var session = await _sessionService.CreateAsync(user, RememberMe, ["pwd"]).ConfigureAwait(false);
        await SignInAsync(user, session.SessionId, RememberMe).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToPage("/Index");
    }

    private const string PartialAuthenticationScheme = "Identity.Partial";

    private async Task SignInPartialAsync(DgDevelopment.Identity.Domain.Entities.User user, bool rememberMe)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString(null, CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim("amr", "pwd"),
                new Claim("remember_me", rememberMe ? "true" : "false"),
            ],
            PartialAuthenticationScheme);

        var properties = new AuthenticationProperties { IsPersistent = rememberMe };
        if (rememberMe)
            properties.ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1);

        await HttpContext.SignInAsync(PartialAuthenticationScheme, new ClaimsPrincipal(identity), properties).ConfigureAwait(false);
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
}