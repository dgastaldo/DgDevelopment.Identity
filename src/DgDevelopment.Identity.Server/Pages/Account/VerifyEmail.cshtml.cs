namespace DgDevelopment.Identity.Server.Pages.Account;

using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public sealed class VerifyEmailModel(
    IVerificationTokenRepository verificationTokenRepository,
    IUserRepository userRepository) : PageModel
{
    public bool Success { get; private set; }

    public async Task OnGetAsync([FromQuery] string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;

        var verification = await verificationTokenRepository.GetByTokenHashAsync(Hash(token)).ConfigureAwait(false);
        if (verification is not { IsUsed: false, Purpose: VerificationTokenPurpose.EmailVerification } || verification.IsExpired())
            return;

        var user = await userRepository.GetByIdAsync(verification.UserId).ConfigureAwait(false);
        if (user?.PrimaryEmail is not { } email)
            return;

        user.VerifyEmail(email);
        await userRepository.UpdateAsync(user).ConfigureAwait(false);
        await verificationTokenRepository.MarkUsedAsync(verification.Id).ConfigureAwait(false);

        Success = true;
    }

    private static string Hash(string token) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
