namespace DgDevelopment.Identity.Server.Pages.Account;

using System.Security.Claims;
using DgDevelopment.Identity.Application.Consent;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.OAuth.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

public sealed class ConsentModel : PageModel
{
    private static readonly Dictionary<string, string> ScopeDescriptions = new(StringComparer.Ordinal)
    {
        ["openid"] = "Sign you in using your DgDevelopment identity.",
        ["profile"] = "View your basic profile information (name, username).",
        ["email"] = "View your email address.",
    };

    private readonly IClientRepository _clientRepository;
    private readonly IConsentService _consentService;
    private readonly IOptions<ConsentOptions> _options;
    private readonly IUserInteractionService _interaction;

    public ConsentModel(
        IClientRepository clientRepository,
        IConsentService consentService,
        IOptions<ConsentOptions> options,
        IUserInteractionService interaction)
    {
        _clientRepository = clientRepository;
        _consentService = consentService;
        _options = options;
        _interaction = interaction;
    }

    [FromQuery]
    public string? ReturnUrl { get; set; }

    public string ClientName { get; private set; } = string.Empty;
    public IReadOnlyCollection<ConsentScopeItem> RequestedScopes { get; private set; } = [];
    public IReadOnlyCollection<string> AdminApprovedScopes { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync([FromQuery] string? returnUrl)
    {
        ReturnUrl = returnUrl;

        if (User.Identity?.IsAuthenticated != true)
            return RedirectToLogin(returnUrl);

        var parsed = ParseReturnUrl(returnUrl);
        if (parsed.ClientId is null)
            return RedirectToPage("/Error", new { errorCode = "invalid_request", errorDescription = "Invalid consent request." });

        var client = await _clientRepository.GetByClientIdAsync(parsed.ClientId).ConfigureAwait(false);
        if (client is null || !client.IsActive)
            return RedirectToPage("/Error", new { errorCode = "invalid_client", errorDescription = "Invalid client." });

        ClientName = client.Name;

        var requested = parsed.Scope?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
        var adminApproved = client.AdminConsentScopes.Select(s => s.Scope).ToHashSet(StringComparer.Ordinal);
        AdminApprovedScopes = requested.Where(adminApproved.Contains).ToList();
        RequestedScopes = _consentService.GetScopesRequiringUserConsent(client, requested)
            .Select(scope => new ConsentScopeItem(scope, ScopeDescription(scope)))
            .ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string action, string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToLogin(returnUrl);

        var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdValue, out var userId) || string.IsNullOrWhiteSpace(returnUrl))
            return RedirectToPage("/Error", new { errorCode = "invalid_request", errorDescription = "Invalid consent request." });

        var parsed = ParseReturnUrl(returnUrl);
        if (parsed.ClientId is null || parsed.RedirectUri is null)
            return RedirectToPage("/Error", new { errorCode = "invalid_request", errorDescription = "Invalid consent request." });

        var client = await _clientRepository.GetByClientIdAsync(parsed.ClientId).ConfigureAwait(false);
        if (client is null || !client.IsActive)
            return RedirectToPage("/Error", new { errorCode = "invalid_client", errorDescription = "Invalid client." });

        if (string.Equals(action, "approve", StringComparison.OrdinalIgnoreCase))
        {
            var requested = parsed.Scope?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
            var needUser = _consentService.GetScopesRequiringUserConsent(client, requested);
            var lifetime = TimeSpan.FromDays(_options.Value.ConsentLifetimeDays);
            await _consentService.RecordConsentAsync(userId, client.Id, needUser, lifetime).ConfigureAwait(false);
            return LocalRedirect(returnUrl);
        }

        if (!client.RedirectUris.Any(r => r.RedirectUri.ToString() == parsed.RedirectUri))
            return RedirectToPage("/Error", new { errorCode = "invalid_request", errorDescription = "Invalid redirect_uri." });

        var separator = parsed.RedirectUri.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        var denyUrl = $"{parsed.RedirectUri}{separator}error=access_denied";
        if (!string.IsNullOrWhiteSpace(parsed.State))
            denyUrl += $"&state={Uri.EscapeDataString(parsed.State)}";
        return Redirect(denyUrl);
    }

    private RedirectResult RedirectToLogin(string? returnUrl)
    {
        var currentUrl = !string.IsNullOrWhiteSpace(returnUrl) ? returnUrl : $"{Request.Path}{Request.QueryString}";
        return Redirect(_interaction.GetLoginUrl(currentUrl));
    }

    private static string ScopeDescription(string scope)
        => ScopeDescriptions.TryGetValue(scope, out var description) ? description : scope;

    private static ConsentRequestParams ParseReturnUrl(string? returnUrl)
    {
        var queryIndex = returnUrl?.IndexOf('?', StringComparison.Ordinal) ?? -1;
        if (queryIndex < 0)
            return ConsentRequestParams.Empty;

        var queryString = returnUrl![(queryIndex + 1)..];
        var query = QueryHelpers.ParseQuery(queryString);

        return new ConsentRequestParams(
            query.TryGetValue("client_id", out var clientId) ? clientId.ToString() : null,
            query.TryGetValue("scope", out var scope) ? scope.ToString() : null,
            query.TryGetValue("redirect_uri", out var redirectUri) ? redirectUri.ToString() : null,
            query.TryGetValue("state", out var state) ? state.ToString() : null);
    }

    private sealed record ConsentRequestParams(string? ClientId, string? Scope, string? RedirectUri, string? State)
    {
        public static ConsentRequestParams Empty { get; } = new(null, null, null, null);
    }
}

public sealed record ConsentScopeItem(string Scope, string Description);