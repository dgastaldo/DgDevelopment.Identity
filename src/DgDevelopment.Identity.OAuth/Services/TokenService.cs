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
    IUserSessionRepository sessionRepo,
    IJwtService jwtService
    //,ISigningKeyRepository signingKeyRepo
    ) : ITokenService
{
    private static readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

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

        var session = await GetOrCreateSessionAsync(user.Id, existingSessionId: null, ct).ConfigureAwait(false);

        return await GenerateTokensAsync(client, user, scopes, null, session, ct).ConfigureAwait(false);
    }

    public async Task<TokenResponse> ProcessClientCredentialsAsync(ClientValidationResult client, string[] scopes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        var accessToken = await jwtService.CreateAccessTokenAsync(new(
            new User("system", "", DgDevelopment.Identity.Domain.ValueObjects.EmailAddress.FromString("system@idp.local")),
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

        var session = await GetOrCreateSessionAsync(user.Id, storedToken.SessionId, ct).ConfigureAwait(false);

        return await GenerateTokensAsync(client, user, scopes, storedToken.Id, session, ct).ConfigureAwait(false);
    }

    public async Task<TokenResponse> ProcessDeviceCodeAsync(string deviceCode, string clientId, CancellationToken ct = default)
    {
        var deviceCodeHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(deviceCode)));
        var storedDevice = await deviceCodeRepo.GetByDeviceCodeHashAsync(deviceCodeHash, ct).ConfigureAwait(false)
            ?? throw new DeviceAuthorizationException("invalid_grant", "Invalid device code.");

        if (storedDevice.IsExpired())
            throw new DeviceAuthorizationException("expired_token", "Device code has expired.");

        if (storedDevice.IsUsed)
            throw new DeviceAuthorizationException("invalid_grant", "Device code has already been used.");

        var now = DateTime.UtcNow;
        await deviceCodeRepo.RecordPollAsync(storedDevice.Id, now, ct).ConfigureAwait(false);

        if (storedDevice.LastPolledAt is { } lastPolledAt && now - lastPolledAt < _pollInterval)
            throw new DeviceAuthorizationException("slow_down", "Device code polling is too frequent.");

        if (!storedDevice.IsAuthorized || storedDevice.UserId == null)
            throw new DeviceAuthorizationException("authorization_pending", "Device code authorization is still pending.");

        var client = (await clientRepo.GetByClientIdAsync(clientId, ct).ConfigureAwait(false))
            ?? throw new DeviceAuthorizationException("invalid_client", "Invalid client.");
        if (client.Id != storedDevice.ClientId)
            throw new DeviceAuthorizationException("invalid_grant", "Device code was not issued to this client.");

        var user = (await userRepo.GetByIdAsync(storedDevice.UserId.Value, ct).ConfigureAwait(false))!;
        var scopes = storedDevice.GetScopes();

        await deviceCodeRepo.MarkAsUsedAsync(storedDevice.Id, ct).ConfigureAwait(false);

        var session = await GetOrCreateSessionAsync(user.Id, existingSessionId: null, ct).ConfigureAwait(false);

        return await GenerateTokensAsync(client, user, scopes, null, session, ct).ConfigureAwait(false);
    }

    private async Task<UserSession> GetOrCreateSessionAsync(Guid userId, Guid? existingSessionId, CancellationToken ct)
    {
        if (existingSessionId is { } sessionId)
        {
            var existing = await sessionRepo.GetByIdAsync(sessionId, ct).ConfigureAwait(false);
            if (existing is not null && !existing.IsRevoked && !existing.IsExpired())
                return existing;
        }

        var session = new UserSession(userId, Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddHours(8), ["pwd"]);
        await sessionRepo.AddAsync(session, ct).ConfigureAwait(false);
        return session;
    }

    private async Task<TokenResponse> GenerateTokensAsync(Client client, User user, string[] scopes, Guid? previousTokenId, UserSession session, CancellationToken ct)
    {
        var sessionId = session.Id.ToString("N");
        var accessToken = await jwtService.CreateAccessTokenAsync(new(user, client, scopes, null), ct).ConfigureAwait(false);
        var idToken = await jwtService.CreateIdTokenAsync(new(user, client, scopes, null, ["pwd"], sessionId), ct).ConfigureAwait(false);

        var refreshTokenValue = DgDevelopment.Identity.Domain.ValueObjects.Secret.Generate(64);
        var refreshTokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refreshTokenValue)));
        var refreshToken = new RefreshToken(refreshTokenHash, client.Id, user.Id, session.Id, scopes, previousTokenId);
        await refreshTokenRepo.AddAsync(refreshToken, ct).ConfigureAwait(false);

        return new(accessToken, "Bearer", 3600, idToken, refreshTokenValue, string.Join(' ', scopes));
    }
}
