using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.OAuth.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DgDevelopment.Identity.Server.Pages.Connect;

public sealed class AuthorizeModel(
    IAuthorizationService authorizationService,
    IUserRepository userRepository) : PageModel
{
    [BindProperty(SupportsGet = true)] public string ClientId { get; set; } = string.Empty;
    [BindProperty(SupportsGet = true)] public string RedirectUri { get; set; } = string.Empty;
    [BindProperty(SupportsGet = true)] public string ResponseType { get; set; } = string.Empty;
    [BindProperty(SupportsGet = true)] public string Scope { get; set; } = string.Empty;
    [BindProperty(SupportsGet = true)] public string? State { get; set; }
    [BindProperty(SupportsGet = true)] public string? Nonce { get; set; }
    [BindProperty(SupportsGet = true)] public string? CodeChallenge { get; set; }
    [BindProperty(SupportsGet = true)] public string? CodeChallengeMethod { get; set; }

    public bool IsAuthenticated { get; set; }
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var result = await authorizationService.ValidateAsync(new(
            ClientId, RedirectUri, ResponseType, Scope, State, Nonce,
            CodeChallenge, CodeChallengeMethod)).ConfigureAwait(false);

        if (!result.IsValid)
            return RedirectToPage("/Error", new { errorCode = result.Error, errorDescription = result.ErrorDescription });

        if (!User.Identity!.IsAuthenticated)
        {
            IsAuthenticated = false;
            return Page();
        }

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        var user = await userRepository.GetByIdAsync(Guid.Parse(userId)).ConfigureAwait(false);
        if (user == null)
            return RedirectToPage("/Error", new { errorCode = "invalid_user", errorDescription = "User not found." });

        var scopes = Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var code = await authorizationService.CreateAuthorizationCodeAsync(
            result.Client!, user, scopes, RedirectUri, CodeChallenge, CodeChallengeMethod).ConfigureAwait(false);

        var redirect = $"{RedirectUri}?code={Uri.EscapeDataString(code)}";
        if (State != null)
            redirect += $"&state={Uri.EscapeDataString(State)}";

        return Redirect(redirect);
    }
}
