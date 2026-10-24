namespace DgDevelopment.Identity.Server.Controllers;

using System.Globalization;
using System.Security.Claims;
using DgDevelopment.Identity.Application.Services;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Server.Hubs;
using DgDevelopment.Identity.Server.Models;
using DgDevelopment.Identity.Server.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

// Bare [Authorize] defaults to the "Cookies" scheme only - fine for this controller's original
// callers (the phone app's push endpoints, cookie-authenticated; the login-time partial-auth
// Razor Page, a different scheme entirely) but the Blazor WASM self-service client only ever
// presents a Bearer token, so it needs the JWT scheme accepted too. Comma-delimited schemes
// succeed if any one matches - additive, doesn't remove Cookie support.
[ApiController]
[Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},Cookies")]
[Route("api/v1/account/mfa")]
public sealed class MfaController : ControllerBase
{
    private readonly IPushMfaService _pushMfaService;
    private readonly IMfaChallengeRepository _challengeRepository;
    private readonly IHubContext<MfaHub> _hubContext;
    private readonly ITotpService _totpService;
    private readonly IUserRepository _userRepository;

    public MfaController(
        IPushMfaService pushMfaService,
        IMfaChallengeRepository challengeRepository,
        IHubContext<MfaHub> hubContext,
        ITotpService totpService,
        IUserRepository userRepository)
    {
        _pushMfaService = pushMfaService;
        _challengeRepository = challengeRepository;
        _hubContext = hubContext;
        _totpService = totpService;
        _userRepository = userRepository;
    }

    [HttpGet("push-devices")]
    public async Task<IActionResult> GetDevices(CancellationToken ct)
    {
        var devices = await _pushMfaService.GetDevicesAsync(GetUserId(), ct).ConfigureAwait(false);
        return Ok(devices);
    }

    [HttpPost("push-devices")]
    public async Task<IActionResult> RegisterDevice(
        [FromBody] RegisterPushDeviceRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _pushMfaService.RegisterDeviceAsync(
            GetUserId(),
            request.Platform ?? string.Empty,
            request.PushToken ?? string.Empty,
            request.DeviceName,
            request.TotpCode,
            ct).ConfigureAwait(false);

        return NoContent();
    }

    [HttpDelete("push-devices/{id:guid}")]
    public async Task<IActionResult> RemoveDevice(Guid id, CancellationToken ct)
    {
        await _pushMfaService.RemoveDeviceAsync(GetUserId(), id, ct).ConfigureAwait(false);
        return NoContent();
    }

    [HttpGet("challenge/{id:guid}")]
    public async Task<IActionResult> GetChallengeStatus(Guid id, CancellationToken ct)
    {
        var challenge = await _challengeRepository.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (challenge is null || challenge.UserId != GetUserId())
            return NotFound();

        var status = await _pushMfaService.GetChallengeStatusAsync(id, ct).ConfigureAwait(false);
        return Ok(new { challengeId = id, status = status.ToString() });
    }

    [HttpPost("push/{challengeId:guid}/approve")]
    public async Task<IActionResult> Approve(
        Guid challengeId,
        [FromBody] ResolveChallengeRequest request,
        CancellationToken ct)
    {
        return await ResolveAsync(challengeId, request?.ChallengeCode, approved: true, ct).ConfigureAwait(false);
    }

    [HttpPost("push/{challengeId:guid}/deny")]
    public async Task<IActionResult> Deny(
        Guid challengeId,
        [FromBody] ResolveChallengeRequest request,
        CancellationToken ct)
    {
        return await ResolveAsync(challengeId, request?.ChallengeCode, approved: false, ct).ConfigureAwait(false);
    }

    private async Task<IActionResult> ResolveAsync(Guid challengeId, string? challengeCode, bool approved, CancellationToken ct)
    {
        var userId = GetUserId();
        var challenge = await _challengeRepository.GetByIdAsync(challengeId, ct).ConfigureAwait(false);
        if (challenge is null || challenge.UserId != userId)
            return NotFound();

        var resolved = approved
            ? await _pushMfaService.ApproveAsync(challengeId, challengeCode ?? string.Empty, ct).ConfigureAwait(false)
            : await _pushMfaService.DenyAsync(challengeId, challengeCode ?? string.Empty, ct).ConfigureAwait(false);

        if (!resolved)
            return BadRequest(new { error = "invalid_challenge", error_description = "The challenge is no longer pending or the code is invalid." });

        var hub = _hubContext.Clients.Group(MfaHub.GroupForUser(userId.ToString(null, CultureInfo.InvariantCulture)));
        await hub.SendAsync("OnChallengeResolved", challengeId, approved, ct).ConfigureAwait(false);

        return Ok(new { approved });
    }

    [HttpGet("totp/status")]
    public async Task<IActionResult> GetTotpStatus(CancellationToken ct)
    {
        var status = await _totpService.GetStatusAsync(GetUserId(), ct).ConfigureAwait(false);
        return Ok(status);
    }

    [HttpPost("totp/enroll")]
    public async Task<IActionResult> EnrollTotp(CancellationToken ct)
    {
        var userId = GetUserId();
        var user = await _userRepository.GetByIdAsync(userId, ct).ConfigureAwait(false);

        try
        {
            var enrollment = await _totpService.EnrollAsync(userId, user?.Username ?? "user", ct).ConfigureAwait(false);
            return Ok(new
            {
                secretKey = enrollment.SecretKey,
                provisioningUri = enrollment.ProvisioningUri.ToString(),
                qrCodeDataUri = TotpQrCodeRenderer.BuildDataUri(enrollment.ProvisioningUri),
            });
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "TOTP already enabled", detail: ex.Message);
        }
    }

    [HttpPost("totp/enable")]
    public async Task<IActionResult> EnableTotp([FromBody] EnableTotpRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var result = await _totpService.EnableAsync(GetUserId(), request.Code ?? string.Empty, ct).ConfigureAwait(false);
            if (!result.IsValid)
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid code", detail: "The verification code is invalid.");

            return Ok(new { backupCodes = result.Codes });
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Cannot enable TOTP", detail: ex.Message);
        }
    }

    [HttpPost("totp/backup-codes/regenerate")]
    public async Task<IActionResult> RegenerateBackupCodes(CancellationToken ct)
    {
        var result = await _totpService.RegenerateBackupCodesAsync(GetUserId(), ct).ConfigureAwait(false);
        if (!result.IsValid)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "TOTP not enabled", detail: "Enable TOTP before regenerating backup codes.");

        return Ok(new { backupCodes = result.Codes });
    }

    [HttpPost("totp/disable")]
    public async Task<IActionResult> DisableTotp(CancellationToken ct)
    {
        await _totpService.DisableAsync(GetUserId(), ct).ConfigureAwait(false);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (value == null || !Guid.TryParse(value, out var userId))
            throw new InvalidOperationException("Missing user identifier in the authentication.");

        return userId;
    }
}
