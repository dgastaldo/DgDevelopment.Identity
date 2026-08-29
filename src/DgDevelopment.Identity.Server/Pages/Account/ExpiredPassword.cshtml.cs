namespace DgDevelopment.Identity.Server.Pages.Account;

using System.Globalization;
using System.Security.Claims;
using DgDevelopment.Identity.Application.Common;
using DgDevelopment.Identity.Application.Services;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Domain.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

[Authorize(AuthenticationSchemes = PartialAuthenticationScheme)]
public sealed class ExpiredPasswordModel(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IPasswordHistoryService passwordHistoryService,
    IMfaPolicyService mfaPolicy,
    IServerSessionService sessionService) : PageModel
{
    private const string PartialAuthenticationScheme = "Identity.Partial";

    [BindProperty] public string Password { get; set; } = string.Empty;
    [BindProperty] public string ConfirmPassword { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }

    public IActionResult OnGet([FromQuery] string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync([FromQuery] string? returnUrl = null)
    {
        ReturnUrl = returnUrl;

        if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
        {
            ModelState.AddModelError(string.Empty, "Passwords do not match.");
            return Page();
        }

        var userId = GetUserId();
        var user = await userRepository.GetByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
            return RedirectToPage("/Account/Login");

        try
        {
            PasswordPolicy.Validate(Password);
            await passwordHistoryService.EnsureNotReusedAsync(user.Id, Password, user.PasswordHash).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }

        var previousHash = user.PasswordHash;
        user.SetPassword(passwordHasher.HashPassword(Password));
        await userRepository.UpdateAsync(user).ConfigureAwait(false);
        await passwordHistoryService.RecordChangeAsync(user.Id, previousHash).ConfigureAwait(false);

        if (await mfaPolicy.RequiresMfaStepAsync(user).ConfigureAwait(false))
            return RedirectToPage("/Account/Mfa", new { returnUrl });

        var rememberMe = User.FindFirst("remember_me")?.Value == "true";
        var session = await sessionService.CreateAsync(user, rememberMe, ["pwd"]).ConfigureAwait(false);
        await SignInAsync(user, session.SessionId, rememberMe).ConfigureAwait(false);
        await HttpContext.SignOutAsync(PartialAuthenticationScheme).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToPage("/Index");
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

    private Guid GetUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (value == null || !Guid.TryParse(value, out var userId))
            throw new InvalidOperationException("Missing user identity in the partial authentication cookie.");

        return userId;
    }
}
