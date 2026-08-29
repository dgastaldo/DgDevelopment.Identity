namespace DgDevelopment.Identity.Server.Pages.Account;

using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Application.Common;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Domain.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public sealed class ResetPasswordModel(
    IVerificationTokenRepository verificationTokenRepository,
    IUserRepository userRepository,
    IPasswordHasher passwordHasher) : PageModel
{
    [BindProperty] public string Token { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;
    [BindProperty] public string ConfirmPassword { get; set; } = string.Empty;

    public bool InvalidToken { get; private set; }
    public bool Success { get; private set; }

    public async Task<IActionResult> OnGetAsync([FromQuery] string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || !await IsValidResetTokenAsync(token).ConfigureAwait(false))
        {
            InvalidToken = true;
            return Page();
        }

        Token = token;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Token) || !await IsValidResetTokenAsync(Token).ConfigureAwait(false))
        {
            InvalidToken = true;
            return Page();
        }

        if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
        {
            ModelState.AddModelError(string.Empty, "Passwords do not match.");
            return Page();
        }

        try
        {
            PasswordPolicy.Validate(Password);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }

        var verification = await verificationTokenRepository.GetByTokenHashAsync(Hash(Token)).ConfigureAwait(false);
        var user = verification is null ? null : await userRepository.GetByIdAsync(verification.UserId).ConfigureAwait(false);
        if (verification is null || user is null)
        {
            InvalidToken = true;
            return Page();
        }

        user.SetPassword(passwordHasher.HashPassword(Password));
        await userRepository.UpdateAsync(user).ConfigureAwait(false);
        await verificationTokenRepository.MarkUsedAsync(verification.Id).ConfigureAwait(false);

        Success = true;
        return Page();
    }

    private async Task<bool> IsValidResetTokenAsync(string token)
    {
        var verification = await verificationTokenRepository.GetByTokenHashAsync(Hash(token)).ConfigureAwait(false);
        return verification is { IsUsed: false, Purpose: VerificationTokenPurpose.PasswordReset } && !verification.IsExpired();
    }

    private static string Hash(string token) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
