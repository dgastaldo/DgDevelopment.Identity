using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.OAuth.Services;

public interface IAuthorizationService
{
    Task<AuthorizationResult> ValidateAsync(AuthorizationRequest request, CancellationToken ct = default);
    Task<string> CreateAuthorizationCodeAsync(Guid tenantId, Client client, User user, string[] scopes, string redirectUri, string? codeChallenge, string? codeChallengeMethod, CancellationToken ct = default);
}
