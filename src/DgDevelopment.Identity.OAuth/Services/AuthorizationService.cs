namespace DgDevelopment.Identity.OAuth.Services;

using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Domain.ValueObjects;

public sealed class AuthorizationService(IClientRepository clientRepository, IAuthorizationCodeRepository codeRepository) : IAuthorizationService
{
    private const string InvalidRequestError = "invalid_request";

    public async Task<AuthorizationResult> ValidateAsync(AuthorizationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.ClientId))
            return new(false, null, InvalidRequestError, "Missing client_id.", null);

        var client = await clientRepository.GetByClientIdAsync(request.ClientId, ct).ConfigureAwait(false);
        if (client == null || !client.IsActive)
            return new(false, null, "invalid_client", "Invalid client.", null);

        if (request.ResponseType != "code")
            return new(false, null, "unsupported_response_type", "Only 'code' response type is supported.", null);

        var requestedScopes = request.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var disallowedScope = requestedScopes.FirstOrDefault(scope => !client.Scopes.Any(s => s.Scope == scope));
        if (disallowedScope is not null)
            return new(false, null, "invalid_scope", $"Scope '{disallowedScope}' not allowed.", null);

        var redirectUri = new Uri(request.RedirectUri);
        if (!client.RedirectUris.Any(r => r.RedirectUri == redirectUri))
            return new(false, null, InvalidRequestError, "Invalid redirect_uri.", null);

        if (client.RequirePkce && string.IsNullOrWhiteSpace(request.CodeChallenge))
            return new(false, null, InvalidRequestError, "PKCE code_challenge is required.", null);

        if (!string.IsNullOrWhiteSpace(request.CodeChallengeMethod) && request.CodeChallengeMethod != "S256")
            return new(false, null, InvalidRequestError, "Only S256 code_challenge_method is supported.", null);

        return new(true, client, null, null, request.RedirectUri);
    }

    public async Task<string> CreateAuthorizationCodeAsync(AuthorizationCodeRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = Secret.Generate(32);
        var codeHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

        var authCode = new AuthorizationCode(
            request.TenantId, codeHash, request.Client.Id, request.User.Id, new Uri(request.RedirectUri),
            request.Scopes, request.CodeChallenge, request.CodeChallengeMethod);
        await codeRepository.AddAsync(authCode, ct).ConfigureAwait(false);

        return code;
    }
}
