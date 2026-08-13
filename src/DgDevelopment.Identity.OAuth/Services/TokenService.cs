namespace DgDevelopment.Identity.OAuth.Services;

using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;

public sealed class TokenService(
    IAuthorizationCodeRepository authCodeRepo,
    IRefreshTokenRepository refreshTokenRepo,
    IDeviceCodeRepository deviceCodeRepo,
    IClientRepository clientRepo,
    IUserRepository userRepo,
    IJwtService jwtService
    //,ISigningKeyRepository signingKeyRepo
    ) : ITokenService
{

    public async Task<TokenResponse> ProcessAuthorizationCodeAsync(string code, string codeVerifier, string clientId, Uri redirectUri, CancellationToken ct = default)
    {
        var codeHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
        var authCode = await authCodeRepo.GetByCodeHashAsync(codeHash, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Invalid authorization code.");

        if (authCode.IsUsed || authCode.IsExpired())
            throw new InvalidOperationException("Authorization code has expired or was already used.");

        if (authCode.ClientId != (await clientRepo.GetByClientIdAsync(clientId, ct).ConfigureAwait(false))!.Id)
            throw new InvalidOperationException("Client mismatch.");

        if (authCode.RedirectUri != redirectUri)
            throw new InvalidOperationException("Redirect URI mismatch.");

        if (authCode.CodeChallengeHash != null)
        {
            var expectedHash = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)))
                .Replace("+", "-", StringComparison.Ordinal)
                .Replace("/", "_", StringComparison.Ordinal)
                .TrimEnd('=');

            if (authCode.CodeChallengeHash != expectedHash)
                throw new InvalidOperationException("Invalid code_verifier.");
        }

        await authCodeRepo.MarkAsUsedAsync(authCode.Id, ct).ConfigureAwait(false);

        var client = (await clientRepo.GetByClientIdAsync(clientId, ct).ConfigureAwait(false))!;
        var user = (await userRepo.GetByIdAsync(authCode.UserId, ct).ConfigureAwait(false))!;
        var scopes = authCode.GetScopes();

        return await GenerateTokensAsync(client, user, scopes, null, ct).ConfigureAwait(false);
    }

    public async Task<TokenResponse> ProcessClientCredentialsAsync(ClientValidationResult client, string[] scopes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        var accessToken = await jwtService.CreateAccessTokenAsync(new(
            new User("system", "", DgDevelopment.Identity.Domain.ValueObjects.EmailAddress.FromString("system@localhost")),
            client.Client!,
            scopes,
            null,
            3600), ct).ConfigureAwait(false);

        return new(accessToken, "Bearer", 3600, null, null, string.Join(' ', scopes));
    }

    public async Task<TokenResponse> ProcessRefreshTokenAsync(string refreshToken, string clientId, CancellationToken ct = default)
    {
        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
        var storedToken = await refreshTokenRepo.GetByTokenHashAsync(tokenHash, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Invalid refresh token.");

        if (storedToken.IsRevoked || storedToken.IsExpired())
            throw new InvalidOperationException("Refresh token has expired or was revoked.");

        var client = (await clientRepo.GetByClientIdAsync(clientId, ct).ConfigureAwait(false))!;
        var user = (await userRepo.GetByIdAsync(storedToken.UserId, ct).ConfigureAwait(false))!;
        var scopes = storedToken.GetScopes();

        await refreshTokenRepo.RevokeAsync(storedToken.Id, ct).ConfigureAwait(false);

        return await GenerateTokensAsync(client, user, scopes, storedToken.Id, ct).ConfigureAwait(false);
    }

    public async Task<TokenResponse> ProcessDeviceCodeAsync(string deviceCode, string clientId, CancellationToken ct = default)
    {
        var deviceCodeHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(deviceCode)));
        var storedDevice = await deviceCodeRepo.GetByDeviceCodeHashAsync(deviceCodeHash, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Invalid device code.");

        if (!storedDevice.IsAuthorized || storedDevice.IsUsed || storedDevice.IsExpired())
            throw new InvalidOperationException("Device code not authorized or expired.");

        if (storedDevice.UserId == null)
            throw new InvalidOperationException("Device code not authorized by user.");

        var client = (await clientRepo.GetByClientIdAsync(clientId, ct).ConfigureAwait(false))!;
        var user = (await userRepo.GetByIdAsync(storedDevice.UserId.Value, ct).ConfigureAwait(false))!;
        var scopes = storedDevice.GetScopes();

        await deviceCodeRepo.MarkAsUsedAsync(storedDevice.Id, ct).ConfigureAwait(false);

        return await GenerateTokensAsync(client, user, scopes, null, ct).ConfigureAwait(false);
    }

    private async Task<TokenResponse> GenerateTokensAsync(Client client, User user, string[] scopes, Guid? previousTokenId, CancellationToken ct)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var accessToken = await jwtService.CreateAccessTokenAsync(new(user, client, scopes, null), ct).ConfigureAwait(false);
        var idToken = await jwtService.CreateIdTokenAsync(new(user, client, scopes, null, ["pwd"], sessionId), ct).ConfigureAwait(false);

        var refreshTokenValue = DgDevelopment.Identity.Domain.ValueObjects.Secret.Generate(64);
        var refreshTokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refreshTokenValue)));
        var refreshToken = new RefreshToken(refreshTokenHash, client.Id, user.Id, Guid.Parse(sessionId), scopes, previousTokenId);
        await refreshTokenRepo.AddAsync(refreshToken, ct).ConfigureAwait(false);

        return new(accessToken, "Bearer", 3600, idToken, refreshTokenValue, string.Join(' ', scopes));
    }
}
