using System.Globalization;
using System.Security.Claims;
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

    public AuthorizeModel(
        IAuthorizationService authorizationService,
        IUserRepository userRepo,
        IServerSessionService sessionService,
        IUserInteractionService interaction)
    {
        _authorizationService = authorizationService;
        _userRepo = userRepo;
        _sessionService = sessionService;
        _interaction = interaction;
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

    public async Task<IActionResult> OnGetAsync(
        [FromQuery] string client_id,
        [FromQuery] string redirect_uri,
        [FromQuery] string response_type,
        [FromQuery] string scope,
        [FromQuery] string? state = null,
        [FromQuery] string? nonce = null,
        [FromQuery] string? code_challenge = null,
        [FromQuery] string? code_challenge_method = null,
        [FromQuery] string? login_hint = null)
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

        return await IssueCodeAsync(result.Client!, userId).ConfigureAwait(false);
    }

    private RedirectResult RedirectToLogin()
    {
        var returnUrl = $"{Request.Path}{Request.QueryString}";
        var loginUrl = _interaction.GetLoginUrl(returnUrl);
        if (!string.IsNullOrWhiteSpace(LoginHint))
            loginUrl += $"&login_hint={Uri.EscapeDataString(LoginHint)}";

        return Redirect(loginUrl);
    }

    private async Task<IActionResult> IssueCodeAsync(DgDevelopment.Identity.Domain.Entities.Client client, Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId).ConfigureAwait(false);
        if (user == null)
            return RedirectToPage("/Error", new { errorCode = "invalid_user", errorDescription = "User not found." });

        var scopes = Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var code = await _authorizationService.CreateAuthorizationCodeAsync(
            client, user, scopes, RedirectUri, CodeChallenge, CodeChallengeMethod).ConfigureAwait(false);

        var redirect = $"{RedirectUri}?code={Uri.EscapeDataString(code)}";
        if (State != null)
            redirect += $"&state={Uri.EscapeDataString(State)}";

        return Redirect(redirect);
    }
}