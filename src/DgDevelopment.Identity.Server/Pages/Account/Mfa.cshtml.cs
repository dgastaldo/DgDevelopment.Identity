namespace DgDevelopment.Identity.Server.Pages.Account;

using System.Globalization;
using System.Security.Claims;
using DgDevelopment.Identity.Application.Services;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Server.Hubs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using QRCoder;

[Authorize(AuthenticationSchemes = "Identity.Partial")]
public sealed class MfaModel : PageModel
{
    private const string PartialAuthenticationScheme = "Identity.Partial";

    private readonly IUserRepository _userRepository;
    private readonly ITotpService _totpService;
    private readonly IPushMfaService _pushMfaService;
    private readonly IServerSessionService _sessionService;
    private readonly IHubContext<MfaHub> _hubContext;

    public MfaModel(
        IUserRepository userRepository,
        ITotpService totpService,
        IPushMfaService pushMfaService,
        IServerSessionService sessionService,
        IHubContext<MfaHub> hubContext)
    {
        _userRepository = userRepository;
        _totpService = totpService;
        _pushMfaService = pushMfaService;
        _sessionService = sessionService;
        _hubContext = hubContext;
    }

    [BindProperty] public string Code { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }

    public bool TotpEnabled { get; private set; }
    public bool HasPushDevices { get; private set; }
    public bool NeedsEnrollment { get; private set; }
    public string? EnrollmentSecret { get; private set; }
    public string? QrCodeDataUri { get; private set; }
    public IReadOnlyCollection<string>? BackupCodes { get; private set; }
    public Guid UserId { get; private set; }

    public async Task<IActionResult> OnGetAsync([FromQuery] string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        return await LoadAsync().ConfigureAwait(false);
    }

    public async Task<IActionResult> OnPostTotpAsync([FromQuery] string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(Code))
        {
            ModelState.AddModelError(string.Empty, "Enter the verification code.");
            return await LoadAsync().ConfigureAwait(false);
        }

        var userId = GetUserId();
        if (!await _totpService.VerifyAsync(userId, Code).ConfigureAwait(false))
        {
            ModelState.AddModelError(string.Empty, "The verification code is invalid or expired.");
            return await LoadAsync().ConfigureAwait(false);
        }

        return await CompleteLoginAsync(userId, ["pwd", "totp"], returnUrl).ConfigureAwait(false);
    }

    public async Task<IActionResult> OnPostEnrollAsync([FromQuery] string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(Code))
        {
            ModelState.AddModelError(string.Empty, "Enter the verification code.");
            return await LoadAsync().ConfigureAwait(false);
        }

        var result = await _totpService.EnableAsync(userId, Code).ConfigureAwait(false);
        if (!result.IsValid)
        {
            ModelState.AddModelError(string.Empty, "The verification code is invalid.");
            return await LoadAsync().ConfigureAwait(false);
        }

        BackupCodes = result.Codes?.ToArray() ?? [];
        TotpEnabled = true;
        HasPushDevices = await _pushMfaService.HasActiveDevicesAsync(userId).ConfigureAwait(false);
        NeedsEnrollment = false;
        return Page();
    }

    public async Task<IActionResult> OnPostStartPushAsync([FromQuery] string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        var userId = GetUserId();

        try
        {
            var created = await _pushMfaService.StartChallengeAsync(userId).ConfigureAwait(false);
            var hub = _hubContext.Clients.Group(MfaHub.GroupForUser(userId.ToString(null, CultureInfo.InvariantCulture)));
            await hub.SendAsync("OnChallenge", created.ChallengeId.ToString()).ConfigureAwait(false);

            return new JsonResult(new { challengeId = created.ChallengeId });
        }
        catch (InvalidOperationException)
        {
            return new JsonResult(new { error = "No active push device is registered." });
        }
    }

    public async Task<IActionResult> OnPostCompletePushAsync([FromQuery] Guid challengeId, [FromQuery] string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        var userId = GetUserId();

        if (!await _pushMfaService.IsChallengeApprovedForUserAsync(challengeId, userId).ConfigureAwait(false))
        {
            ModelState.AddModelError(string.Empty, "The push request has not been approved.");
            return await LoadAsync().ConfigureAwait(false);
        }

        return await CompleteLoginAsync(userId, ["pwd", "push"], returnUrl).ConfigureAwait(false);
    }

    public async Task<IActionResult> OnGetChallengeStatusAsync([FromQuery] Guid challengeId)
    {
        var userId = GetUserId();
        var status = await _pushMfaService.GetChallengeStatusAsync(challengeId).ConfigureAwait(false);
        return new JsonResult(new { status = status.ToString(), approved = status == Domain.Entities.MfaChallengeStatus.Approved });
    }

    private async Task<IActionResult> LoadAsync()
    {
        var userId = GetUserId();
        UserId = userId;

        TotpEnabled = await _totpService.IsEnabledAsync(userId).ConfigureAwait(false);
        HasPushDevices = await _pushMfaService.HasActiveDevicesAsync(userId).ConfigureAwait(false);

        if (!TotpEnabled && !HasPushDevices)
        {
            NeedsEnrollment = true;
            var user = await _userRepository.GetByIdAsync(userId).ConfigureAwait(false);
            var enrollment = await _totpService.EnrollAsync(userId, user?.Username ?? "user").ConfigureAwait(false);
            EnrollmentSecret = enrollment.SecretKey;
            QrCodeDataUri = BuildQrCodeDataUri(enrollment.ProvisioningUri);
        }

        return Page();
    }

    private async Task<IActionResult> CompleteLoginAsync(Guid userId, string[] authMethods, string? returnUrl)
    {
        var user = await _userRepository.GetByIdAsync(userId).ConfigureAwait(false);
        if (user == null)
            return RedirectToPage("/Account/Login");

        var rememberMe = User.FindFirst("remember_me")?.Value == "true";
        var session = await _sessionService.CreateAsync(user, rememberMe, authMethods).ConfigureAwait(false);

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString(null, CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim("session_id", session.SessionId),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        var properties = new AuthenticationProperties { IsPersistent = rememberMe };
        if (rememberMe)
            properties.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), properties).ConfigureAwait(false);
        await HttpContext.SignOutAsync(PartialAuthenticationScheme).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToPage("/Index");
    }

    private Guid GetUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (value == null || !Guid.TryParse(value, out var userId))
            throw new InvalidOperationException("Missing user identity in the partial authentication cookie.");

        return userId;
    }

    private static string BuildQrCodeDataUri(Uri provisioningUri)
    {
        using var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(provisioningUri.ToString(), QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(data);
        return $"data:image/png;base64,{Convert.ToBase64String(qrCode.GetGraphic(20))}";
    }
}
