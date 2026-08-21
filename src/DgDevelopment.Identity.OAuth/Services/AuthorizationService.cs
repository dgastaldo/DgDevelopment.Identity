namespace DgDevelopment.Identity.OAuth.Services;

using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Domain.ValueObjects;

public sealed class AuthorizationService(IClientRepository clientRepository, IAuthorizationCodeRepository codeRepository) : IAuthorizationService
{

    public async Task<AuthorizationResult> ValidateAsync(AuthorizationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.ClientId))
            return new(false, null, "invalid_request", "Missing client_id.", null);

        var client = await clientRepository.GetByClientIdAsync(request.ClientId, ct).ConfigureAwait(false);
        if (client == null || !client.IsActive)
            return new(false, null, "invalid_client", "Invalid client.", null);

        if (request.ResponseType != "code")
            return new(false, null, "unsupported_response_type", "Only 'code' response type is supported.", null);

        var requestedScopes = request.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var scope in requestedScopes)
        {
            if (!client.Scopes.Any(s => s.Scope == scope))
                return new(false, null, "invalid_scope", $"Scope '{scope}' not allowed.", null);
        }

        var redirectUri = new Uri(request.RedirectUri);
        if (!client.RedirectUris.Any(r => r.RedirectUri == redirectUri))
            return new(false, null, "invalid_request", "Invalid redirect_uri.", null);

        if (client.RequirePkce && string.IsNullOrWhiteSpace(request.CodeChallenge))
            return new(false, null, "invalid_request", "PKCE code_challenge is required.", null);

        if (!string.IsNullOrWhiteSpace(request.CodeChallengeMethod) && request.CodeChallengeMethod != "S256")
            return new(false, null, "invalid_request", "Only S256 code_challenge_method is supported.", null);

        return new(true, client, null, null, request.RedirectUri);
    }

    public async Task<string> CreateAuthorizationCodeAsync(Guid tenantId, Client client, User user, string[] scopes, string redirectUri, string? codeChallenge, string? codeChallengeMethod, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(user);
        var code = Secret.Generate(32);
        var codeHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

        var authCode = new AuthorizationCode(tenantId, codeHash, client.Id, user.Id, new Uri(redirectUri), scopes, codeChallenge, codeChallengeMethod);
        await codeRepository.AddAsync(authCode, ct).ConfigureAwait(false);

        return code;
    }
}
