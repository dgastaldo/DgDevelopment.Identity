using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Application.Authorization;
using DgDevelopment.Identity.Application.Common;
using DgDevelopment.Identity.Application.Services;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Domain.Services;
using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Server.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DgDevelopment.Identity.Server.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/v1/me")]
public sealed class MeController(
    ITenantRepository tenantRepository,
    ITenantContext tenantContext,
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IPasswordHistoryService passwordHistoryService,
    ISessionRevocationService sessionRevocationService,
    IVerificationTokenRepository verificationTokenRepository,
    INotificationService notificationService) : ControllerBase
{
    [HttpGet("tenants")]
    public async Task<IActionResult> GetTenants(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var tenants = await tenantRepository.GetTenantsForUserAsync(userId.Value, ct).ConfigureAwait(false);
        var isGlobalAdministrator = await tenantContext.IsGlobalAdministratorAsync(ct).ConfigureAwait(false);

        return Ok(new MyTenantsResponse(
            tenants.Select(t => new TenantResponse(t.Id, t.Name, t.Slug)).ToList(),
            tenantContext.TenantId,
            isGlobalAdministrator));
    }

    [HttpGet]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var user = await userRepository.GetByIdAsync(userId.Value, ct).ConfigureAwait(false);
        if (user is null)
            return Unauthorized();

        return Ok(ToResponse(user));
    }

    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var user = await userRepository.GetByIdAsync(userId.Value, ct).ConfigureAwait(false);
        if (user is null)
            return Unauthorized();

        if (string.IsNullOrEmpty(request.CurrentPassword) || !passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Incorrect password", detail: "The current password is incorrect.");

        try
        {
            PasswordPolicy.Validate(request.NewPassword!);
            await passwordHistoryService.EnsureNotReusedAsync(user.Id, request.NewPassword!, user.PasswordHash, ct).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid password", detail: ex.Message);
        }

        var previousHash = user.PasswordHash;
        user.SetPassword(passwordHasher.HashPassword(request.NewPassword!));
        await userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
        await passwordHistoryService.RecordChangeAsync(user.Id, previousHash, ct).ConfigureAwait(false);
        await sessionRevocationService.RevokeAllAsync(user.Id, ct).ConfigureAwait(false);

        return NoContent();
    }

    [HttpPost("emails")]
    public async Task<IActionResult> AddEmail([FromBody] AddEmailRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Email) || !EmailAddress.IsValid(request.Email))
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid email", detail: "Enter a valid email address.");

        if (await userRepository.GetByEmailAsync(request.Email, ct).ConfigureAwait(false) is not null)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Email already in use", detail: "That email address is already registered.");

        var email = EmailAddress.FromString(request.Email);
        await userRepository.AddEmailAsync(userId.Value, email, isPrimary: false, ct).ConfigureAwait(false);
        await SendVerificationEmailAsync(userId.Value, email, ct).ConfigureAwait(false);

        var updatedUser = await userRepository.GetByIdAsync(userId.Value, ct).ConfigureAwait(false);
        var added = updatedUser?.Emails.First(e => e.Email.Value == email.Value);
        return Created(string.Empty, new MeEmailResponse(added?.Id ?? Guid.Empty, email.Value, IsPrimary: false, IsVerified: false));
    }

    [HttpDelete("emails/{email}")]
    public async Task<IActionResult> RemoveEmail(string email, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var user = await userRepository.GetByIdAsync(userId.Value, ct).ConfigureAwait(false);
        if (user is null)
            return Unauthorized();

        var target = EmailAddress.FromString(email);
        var existing = user.Emails.FirstOrDefault(e => e.Email.Value == target.Value);
        if (existing is { IsPrimary: true } && user.Emails.Count > 1)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Cannot remove primary email",
                detail: "Set another email as primary before removing this one.");

        try
        {
            await userRepository.RemoveEmailAsync(userId.Value, target, ct).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Cannot remove email", detail: ex.Message);
        }

        return NoContent();
    }

    [HttpPut("emails/{email}/primary")]
    public async Task<IActionResult> SetPrimaryEmail(string email, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        try
        {
            await userRepository.SetPrimaryEmailAsync(userId.Value, EmailAddress.FromString(email), ct).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Cannot set primary email", detail: ex.Message);
        }

        return NoContent();
    }

    private async Task SendVerificationEmailAsync(Guid userId, EmailAddress email, CancellationToken ct)
    {
        var token = RandomNumberGenerator.GetHexString(64);
        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        await verificationTokenRepository.AddAsync(new VerificationToken(userId, VerificationTokenPurpose.EmailVerification, tokenHash, email.Value), ct).ConfigureAwait(false);

        var verifyUrl = Url.PageLink("/Account/VerifyEmail", values: new { token }) ?? string.Empty;
        await notificationService.SendEmailAsync(email.Value, "Verify your email address", $"Verify your email address by visiting: {verifyUrl}", ct).ConfigureAwait(false);
    }

    private static MeResponse ToResponse(User user) => new(
        user.Id,
        user.Username,
        user.Emails.Select(e => new MeEmailResponse(e.Id, e.Email.Value, e.IsPrimary, e.IsVerified)).ToList(),
        user.RequireMfa,
        user.CreatedAt);

    private Guid? GetUserId()
    {
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(subject, out var userId) ? userId : null;
    }
}

public sealed record TenantResponse(Guid Id, string Name, string Slug);

public sealed record MyTenantsResponse(IReadOnlyCollection<TenantResponse> Tenants, Guid ActiveTenantId, bool IsGlobalAdministrator);
