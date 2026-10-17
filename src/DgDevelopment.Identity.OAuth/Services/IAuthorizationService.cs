using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.OAuth.Services;

public sealed record AuthorizationCodeRequest(
    Guid TenantId, Client Client, User User, string[] Scopes, string RedirectUri,
    string? CodeChallenge = null, string? CodeChallengeMethod = null);

public interface IAuthorizationService
{
    Task<AuthorizationResult> ValidateAsync(AuthorizationRequest request, CancellationToken ct = default);
    Task<string> CreateAuthorizationCodeAsync(AuthorizationCodeRequest request, CancellationToken ct = default);
}
