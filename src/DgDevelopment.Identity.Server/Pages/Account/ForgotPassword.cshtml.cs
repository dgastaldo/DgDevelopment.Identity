namespace DgDevelopment.Identity.Server.Pages.Account;

using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Domain.Services;
using DgDevelopment.Identity.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

[EnableRateLimiting("auth")]
public sealed class ForgotPasswordModel(
    IUserRepository userRepository,
    IVerificationTokenRepository verificationTokenRepository,
    INotificationService notificationService) : PageModel
{
    [BindProperty] public string Email { get; set; } = string.Empty;

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            ModelState.AddModelError(string.Empty, "Enter your email address.");
            return Page();
        }

        var user = await userRepository.GetByEmailAsync(Email).ConfigureAwait(false);
        if (user is not null)
        {
            var token = RandomNumberGenerator.GetHexString(64);
            var tokenHash = Hash(token);
            var targetEmail = EmailAddress.FromString(Email).Value;
            await verificationTokenRepository.AddAsync(new VerificationToken(user.Id, VerificationTokenPurpose.PasswordReset, tokenHash, targetEmail, lifetimeMinutes: 30)).ConfigureAwait(false);

            var resetUrl = Url.PageLink("/Account/ResetPassword", values: new { token }) ?? string.Empty;
            await notificationService.SendEmailAsync(Email, "Reset your password", $"Reset your password by visiting: {resetUrl}").ConfigureAwait(false);
        }

        // Same response whether or not the account exists - avoids leaking which email
        // addresses are registered.
        return RedirectToPage("/Account/CheckEmail", new { purpose = "reset" });
    }

    private static string Hash(string token) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
