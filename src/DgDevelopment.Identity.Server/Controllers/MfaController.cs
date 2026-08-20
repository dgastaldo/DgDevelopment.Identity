namespace DgDevelopment.Identity.Server.Controllers;

using System.Globalization;
using System.Security.Claims;
using DgDevelopment.Identity.Application.Services;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Server.Hubs;
using DgDevelopment.Identity.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

[ApiController]
[Authorize]
[Route("api/v1/account/mfa")]
public sealed class MfaController : ControllerBase
{
    private readonly IPushMfaService _pushMfaService;
    private readonly IMfaChallengeRepository _challengeRepository;
    private readonly IHubContext<MfaHub> _hubContext;

    public MfaController(
        IPushMfaService pushMfaService,
        IMfaChallengeRepository challengeRepository,
        IHubContext<MfaHub> hubContext)
    {
        _pushMfaService = pushMfaService;
        _challengeRepository = challengeRepository;
        _hubContext = hubContext;
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

    private Guid GetUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (value == null || !Guid.TryParse(value, out var userId))
            throw new InvalidOperationException("Missing user identifier in the authentication cookie.");

        return userId;
    }
}
