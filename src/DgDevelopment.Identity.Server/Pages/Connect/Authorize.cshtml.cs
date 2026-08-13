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
    private readonly IUserAuthenticationService _authService;
    private readonly IUserSessionRepository _sessionRepo;
    private readonly IUserRepository _userRepo;

    public AuthorizeModel(
        IAuthorizationService authorizationService,
        IUserAuthenticationService authService,
        IUserSessionRepository sessionRepo,
        IUserRepository userRepo)
    {
        _authorizationService = authorizationService;
        _authService = authService;
        _sessionRepo = sessionRepo;
        _userRepo = userRepo;
    }

    [BindProperty] public string ClientId { get; set; } = string.Empty;
    [BindProperty] public string RedirectUri { get; set; } = string.Empty;
    [BindProperty] public string ResponseType { get; set; } = string.Empty;
    [BindProperty] public string Scope { get; set; } = string.Empty;
    [BindProperty] public string? State { get; set; }
    [BindProperty] public string? Nonce { get; set; }
    [BindProperty] public string? CodeChallenge { get; set; }
    [BindProperty] public string? CodeChallengeMethod { get; set; }

    [BindProperty] public string Username { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;

    public bool IsAuthenticated { get; set; }
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(
        [FromQuery] string client_id,
        [FromQuery] string redirect_uri,
        [FromQuery] string response_type,
        [FromQuery] string scope,
        [FromQuery] string? state = null,
        [FromQuery] string? nonce = null,
        [FromQuery] string? code_challenge = null,
        [FromQuery] string? code_challenge_method = null)
    {
        ClientId = client_id;
        RedirectUri = redirect_uri;
        ResponseType = response_type;
        Scope = scope;
        State = state;
        Nonce = nonce;
        CodeChallenge = code_challenge;
        CodeChallengeMethod = code_challenge_method;

        var result = await _authorizationService.ValidateAsync(new(
            ClientId, RedirectUri, ResponseType, Scope, State, Nonce,
            CodeChallenge, CodeChallengeMethod)).ConfigureAwait(false);

        if (!result.IsValid)
            return RedirectToPage("/Error", new { errorCode = result.Error, errorDescription = result.ErrorDescription });

        if (!User.Identity!.IsAuthenticated)
        {
            IsAuthenticated = false;
            return Page();
        }

        var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdValue, out var userId))
            return RedirectToPage("/Error", new { errorCode = "invalid_user", errorDescription = "User not found." });

        if (await _userRepo.GetByIdAsync(userId).ConfigureAwait(false) == null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
            IsAuthenticated = false;
            return Page();
        }

        return await IssueCodeAsync(result.Client!, userId).ConfigureAwait(false);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var result = await _authorizationService.ValidateAsync(new(
            ClientId, RedirectUri, ResponseType, Scope, State, Nonce,
            CodeChallenge, CodeChallengeMethod)).ConfigureAwait(false);

        if (!result.IsValid)
            return RedirectToPage("/Error", new { errorCode = result.Error, errorDescription = result.ErrorDescription });

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            Error = "Username and password are required.";
            IsAuthenticated = false;
            return Page();
        }

        var user = await _authService.ValidateCredentialsAsync(Username, Password).ConfigureAwait(false);
        if (user == null)
        {
            Error = "Invalid username or password.";
            IsAuthenticated = false;
            return Page();
        }

        await _authService.RecordSuccessfulLoginAsync(user).ConfigureAwait(false);

        var sessionId = Guid.NewGuid().ToString("N");
        var session = new DgDevelopment.Identity.Domain.Entities.UserSession(user.Id, sessionId, DateTime.UtcNow.AddHours(8), ["pwd"]);
        await _sessionRepo.AddAsync(session).ConfigureAwait(false);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString(null, CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.Username),
            new("session_id", sessionId),
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal).ConfigureAwait(false);

        return await IssueCodeAsync(result.Client!, user.Id).ConfigureAwait(false);
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
