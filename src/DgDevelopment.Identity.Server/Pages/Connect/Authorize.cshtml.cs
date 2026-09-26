using System.Globalization;
using System.Security.Claims;
using DgDevelopment.Identity.Application.Authorization;
using DgDevelopment.Identity.Application.Consent;
using DgDevelopment.Identity.Application.Services;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.OAuth.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DgDevelopment.Identity.Server.Pages.Connect;

public sealed class AuthorizeModel : PageModel
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IUserRepository _userRepo;
    private readonly IServerSessionService _sessionService;
    private readonly IUserInteractionService _interaction;
    private readonly IUserConsentRepository _consentRepository;
    private readonly IConsentService _consentService;
    private readonly ITenantSelectionService _tenantSelectionService;
    private readonly IMfaEnforcementService _mfaEnforcement;

    public AuthorizeModel(
        IAuthorizationService authorizationService,
        IUserRepository userRepo,
        IServerSessionService sessionService,
        IUserInteractionService interaction,
        IUserConsentRepository consentRepository,
        IConsentService consentService,
        ITenantSelectionService tenantSelectionService,
        IMfaEnforcementService mfaEnforcement)
    {
        _authorizationService = authorizationService;
        _userRepo = userRepo;
        _sessionService = sessionService;
        _interaction = interaction;
        _consentRepository = consentRepository;
        _consentService = consentService;
        _tenantSelectionService = tenantSelectionService;
        _mfaEnforcement = mfaEnforcement;
    }

    public string ClientId { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string ResponseType { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string? State { get; set; }
    public string? Nonce { get; set; }
    public string? CodeChallenge { get; set; }
    public string? CodeChallengeMethod { get; set; }
    public string? LoginHint { get; set; }
    public string? Tenant { get; set; }

    public async Task<IActionResult> OnGetAsync(
        [FromQuery] string client_id,
        [FromQuery] string redirect_uri,
        [FromQuery] string response_type,
        [FromQuery] string scope,
        [FromQuery] string? state = null,
        [FromQuery] string? nonce = null,
        [FromQuery] string? code_challenge = null,
        [FromQuery] string? code_challenge_method = null,
        [FromQuery] string? login_hint = null,
        [FromQuery] string? tenant = null)
    {
        ClientId = client_id;
        RedirectUri = redirect_uri;
        ResponseType = response_type;
        Scope = scope;
        State = state;
        Nonce = nonce;
        CodeChallenge = code_challenge;
        CodeChallengeMethod = code_challenge_method;
        LoginHint = login_hint;
        Tenant = tenant;

        var result = await _authorizationService.ValidateAsync(new(
            ClientId, RedirectUri, ResponseType, Scope, State, Nonce,
            CodeChallenge, CodeChallengeMethod)).ConfigureAwait(false);

        if (!result.IsValid)
            return RedirectToPage("/Error", new { errorCode = result.Error, errorDescription = result.ErrorDescription });

        if (!User.Identity!.IsAuthenticated)
            return RedirectToLogin();

        var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdValue, out var userId))
            return RedirectToPage("/Error", new { errorCode = "invalid_user", errorDescription = "User not found." });

        if (await _userRepo.GetByIdAsync(userId).ConfigureAwait(false) == null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
            return RedirectToLogin();
        }

        var activeSession = await _sessionService.FindActiveAsync(userId).ConfigureAwait(false);
        if (activeSession == null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
            return RedirectToLogin();
        }

        var selection = await _tenantSelectionService.ResolveAsync(userId, Tenant).ConfigureAwait(false);
        if (selection.Denied)
            return RedirectToPage("/Error", new { errorCode = "access_denied", errorDescription = "The user does not belong to any active tenant." });

        if (selection.TenantId is null)
        {
            var returnUrl = $"{Request.Path}{Request.QueryString}";
            return RedirectToPage("/Account/SelectTenant", new { returnUrl });
        }

        if (await _mfaEnforcement.MustEnrollMfaBeforeProceedingAsync(userId, selection.TenantId.Value).ConfigureAwait(false))
        {
            var returnUrl = $"{Request.Path}{Request.QueryString}";
            return RedirectToPage("/Account/MfaEnroll", new { returnUrl });
        }

        var client = result.Client!;
        var requestedScopes = Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var stored = await _consentRepository.GetAsync(userId, client.Id).ConfigureAwait(false);
        if (_consentService.NeedsConsent(client, stored, requestedScopes, DateTime.UtcNow))
            return Redirect(_interaction.GetConsentUrl($"{Request.Path}{Request.QueryString}"));

        return await IssueCodeAsync(client, userId, selection.TenantId.Value).ConfigureAwait(false);
    }

    private RedirectResult RedirectToLogin()
    {
        var returnUrl = $"{Request.Path}{Request.QueryString}";
        var loginUrl = _interaction.GetLoginUrl(returnUrl);
        if (!string.IsNullOrWhiteSpace(LoginHint))
            loginUrl += $"&login_hint={Uri.EscapeDataString(LoginHint)}";

        return Redirect(loginUrl);
    }

    private async Task<IActionResult> IssueCodeAsync(DgDevelopment.Identity.Domain.Entities.Client client, Guid userId, Guid tenantId)
    {
        var user = await _userRepo.GetByIdAsync(userId).ConfigureAwait(false);
        if (user == null)
            return RedirectToPage("/Error", new { errorCode = "invalid_user", errorDescription = "User not found." });

        var scopes = Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var code = await _authorizationService.CreateAuthorizationCodeAsync(
            tenantId, client, user, scopes, RedirectUri, CodeChallenge, CodeChallengeMethod).ConfigureAwait(false);

        var redirect = $"{RedirectUri}?code={Uri.EscapeDataString(code)}";
        if (State != null)
            redirect += $"&state={Uri.EscapeDataString(State)}";

        return Redirect(redirect);
    }
}