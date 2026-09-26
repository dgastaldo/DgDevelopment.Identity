namespace DgDevelopment.Identity.Server.Pages.Account;

using System.Security.Claims;
using DgDevelopment.Identity.Application.Services;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Server.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

[Authorize]
[EnableRateLimiting("auth")]
public sealed class MfaEnrollModel(ITotpService totpService, IUserRepository userRepository) : PageModel
{
    [BindProperty] public string Code { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
    public string? EnrollmentSecret { get; private set; }
    public string? QrCodeDataUri { get; private set; }
    public IReadOnlyCollection<string>? BackupCodes { get; private set; }
    public bool Enrolled { get; private set; }

    public Task<IActionResult> OnGetAsync([FromQuery] string? returnUrl = null)
    {
        ReturnUrl = SanitizeReturnUrl(returnUrl);
        return LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync([FromQuery] string? returnUrl = null)
    {
        ReturnUrl = SanitizeReturnUrl(returnUrl);

        if (string.IsNullOrWhiteSpace(Code))
        {
            ModelState.AddModelError(string.Empty, "Enter the verification code.");
            return await LoadAsync().ConfigureAwait(false);
        }

        var userId = GetUserId();
        var result = await totpService.EnableAsync(userId, Code).ConfigureAwait(false);
        if (!result.IsValid)
        {
            ModelState.AddModelError(string.Empty, "The verification code is invalid.");
            return await LoadAsync().ConfigureAwait(false);
        }

        BackupCodes = result.Codes?.ToArray() ?? [];
        Enrolled = true;
        return Page();
    }

    private async Task<IActionResult> LoadAsync()
    {
        var userId = GetUserId();
        var user = await userRepository.GetByIdAsync(userId).ConfigureAwait(false);
        var enrollment = await totpService.EnrollAsync(userId, user?.Username ?? "user").ConfigureAwait(false);
        EnrollmentSecret = enrollment.SecretKey;
        QrCodeDataUri = TotpQrCodeRenderer.BuildDataUri(enrollment.ProvisioningUri);
        return Page();
    }

    private Guid GetUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (value == null || !Guid.TryParse(value, out var userId))
            throw new InvalidOperationException("Missing user identity.");

        return userId;
    }

    // CodeQL cs/web/xss on the form's asp-route-returnUrl in MfaEnroll.cshtml: ASP.NET Core's
    // asp-route-* tag helpers already URL-encode this value when building the form action, so
    // it's very likely a false positive there - but rejecting anything that isn't a genuine local
    // path here removes the query-string value from the picture entirely, regardless.
    private string? SanitizeReturnUrl(string? returnUrl)
        => !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : null;
}
