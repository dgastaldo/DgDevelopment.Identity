namespace DgDevelopment.Identity.Server.Pages.Account;

using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Application.Common;
using DgDevelopment.Identity.Application.Tenants;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Domain.Services;
using DgDevelopment.Identity.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

[EnableRateLimiting("auth")]
public sealed class RegisterModel(
    IUserRepository userRepository,
    ITenantRepository tenantRepository,
    ITenantProvisioningService provisioningService,
    IPasswordHasher passwordHasher,
    IVerificationTokenRepository verificationTokenRepository,
    INotificationService notificationService) : PageModel
{
    [BindProperty] public string Username { get; set; } = string.Empty;
    [BindProperty] public string Email { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;
    [BindProperty] public string ConfirmPassword { get; set; } = string.Empty;
    [BindProperty] public string OrganizationName { get; set; } = string.Empty;
    [BindProperty] public string OrganizationSlug { get; set; } = string.Empty;

    public string? TenantSlug { get; private set; }
    public string? TenantName { get; private set; }
    public bool IsJoiningExistingTenant => !string.IsNullOrWhiteSpace(TenantSlug);

    public async Task<IActionResult> OnGetAsync([FromQuery] string? tenant)
    {
        if (!string.IsNullOrWhiteSpace(tenant))
        {
            var existing = await tenantRepository.GetBySlugAsync(tenant).ConfigureAwait(false);
            if (existing is not { IsActive: true })
                return RedirectToPage("/Error", new { errorCode = "invalid_request", errorDescription = "Unknown organization." });

            TenantSlug = existing.Slug;
            TenantName = existing.Name;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync([FromQuery] string? tenant)
    {
        Tenant? joiningTenant = null;
        if (!string.IsNullOrWhiteSpace(tenant))
        {
            joiningTenant = await tenantRepository.GetBySlugAsync(tenant).ConfigureAwait(false);
            if (joiningTenant is not { IsActive: true })
                return RedirectToPage("/Error", new { errorCode = "invalid_request", errorDescription = "Unknown organization." });

            TenantSlug = joiningTenant.Slug;
            TenantName = joiningTenant.Name;
        }

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ModelState.AddModelError(string.Empty, "Fill in all required fields.");
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

        if (!EmailAddress.IsValid(Email))
        {
            ModelState.AddModelError(string.Empty, "Enter a valid email address.");
            return Page();
        }

        if (await userRepository.GetByUsernameAsync(Username).ConfigureAwait(false) is not null)
        {
            ModelState.AddModelError(string.Empty, "That username is already taken.");
            return Page();
        }

        if (await userRepository.GetByEmailAsync(Email).ConfigureAwait(false) is not null)
        {
            ModelState.AddModelError(string.Empty, "That email address is already registered.");
            return Page();
        }

        var user = new User(Username, passwordHasher.HashPassword(Password), EmailAddress.FromString(Email));

        if (joiningTenant is not null)
        {
            // Joining an existing tenant never grants an elevated role by itself - an existing
            // tenant admin grants roles afterward, matching how every other member is onboarded.
            await userRepository.AddAsync(user).ConfigureAwait(false);
            await tenantRepository.AddMembershipAsync(new TenantMembership(joiningTenant.Id, user.Id)).ConfigureAwait(false);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(OrganizationName) || string.IsNullOrWhiteSpace(OrganizationSlug))
            {
                ModelState.AddModelError(string.Empty, "Enter an organization name and slug.");
                return Page();
            }

            TenantProvisioningResult provisioned;
            try
            {
                provisioned = await provisioningService.ProvisionAsync(OrganizationName, OrganizationSlug).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return Page();
            }

            // The user who creates a new tenant becomes its owner and its GlobalAdmin - mirrors
            // TenantProvisioningService's own bootstrap-tenant pattern (see DbSeeder).
            user.AddToGroup(provisioned.GlobalAdminsGroup);
            await userRepository.AddAsync(user).ConfigureAwait(false);
            await tenantRepository.AddMembershipAsync(new TenantMembership(provisioned.Tenant.Id, user.Id, isOwner: true)).ConfigureAwait(false);
        }

        await SendVerificationEmailAsync(user).ConfigureAwait(false);

        return RedirectToPage("/Account/CheckEmail", new { purpose = "register" });
    }

    private async Task SendVerificationEmailAsync(User user)
    {
        var token = RandomNumberGenerator.GetHexString(64);
        var tokenHash = Hash(token);
        var targetEmail = EmailAddress.FromString(Email).Value;
        await verificationTokenRepository.AddAsync(new VerificationToken(user.Id, VerificationTokenPurpose.EmailVerification, tokenHash, targetEmail)).ConfigureAwait(false);

        var verifyUrl = Url.PageLink("/Account/VerifyEmail", values: new { token }) ?? string.Empty;
        await notificationService.SendEmailAsync(Email, "Verify your email address", $"Welcome! Verify your email address by visiting: {verifyUrl}").ConfigureAwait(false);
    }

    private static string Hash(string token) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
