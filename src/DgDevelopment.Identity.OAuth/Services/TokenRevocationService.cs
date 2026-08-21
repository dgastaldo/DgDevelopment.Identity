namespace DgDevelopment.Identity.OAuth.Services;

using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using Microsoft.IdentityModel.Tokens;

public sealed class TokenRevocationService(
    IRefreshTokenRepository refreshTokenRepo,
    IRevokedTokenRepository revokedTokenRepo,
    IJwtService jwtService,
    IKeyMaterialService keyMaterial,
    IOidcIssuerProvider issuerProvider) : ITokenRevocationService
{
    public async Task RevokeAsync(ClientValidationResult client, string token, string? tokenTypeHint = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(token);
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Token is missing.");

        if (tokenTypeHint == "refresh_token")
        {
            await RevokeRefreshTokenAsync(client, token, ct).ConfigureAwait(false);
            return;
        }

        if (tokenTypeHint == "access_token")
        {
            await RevokeAccessTokenAsync(client, token, ct).ConfigureAwait(false);
            return;
        }

        var revokedRefresh = await RevokeRefreshTokenAsync(client, token, ct).ConfigureAwait(false);
        if (revokedRefresh)
            return;

        await RevokeAccessTokenAsync(client, token, ct).ConfigureAwait(false);
    }

    private async Task<bool> RevokeRefreshTokenAsync(ClientValidationResult client, string token, CancellationToken ct)
    {
        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var stored = await refreshTokenRepo.GetByTokenHashAsync(tokenHash, ct).ConfigureAwait(false);
        if (stored is null)
            return false;

        if (stored.ClientId == client.Client?.Id)
            await refreshTokenRepo.RevokeChainAsync(tokenHash, ct).ConfigureAwait(false);

        return true;
    }

    private async Task<bool> RevokeAccessTokenAsync(ClientValidationResult client, string token, CancellationToken ct)
    {
        var keys = await keyMaterial.GetJwksDocumentAsync(ct).ConfigureAwait(false);
        var issuer = issuerProvider.GetIssuer().GetLeftPart(UriPartial.Authority);
        var parameters = new TokenValidationParameters
        {
            ValidIssuer = issuer,
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = false,
            IssuerSigningKeys = keys.Keys
        };

        ClaimsPrincipal principal;
        try
        {
            principal = await jwtService.ValidateTokenAsync(token, parameters, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            return false;
        }

        var tokenClientId = principal.FindFirst("client_id")?.Value
            ?? principal.FindFirst(JwtRegisteredClaimNames.Aud)?.Value;
        if (tokenClientId != client.Client?.ClientId.ToString())
            return true;

        var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        if (jti is null)
            return true;

        var expClaim = principal.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
        var expiresAt = long.TryParse(expClaim, NumberStyles.None, CultureInfo.InvariantCulture, out var exp)
            ? DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime
            : DateTime.UtcNow.AddHours(1);

        var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid? userId = Guid.TryParse(sub, out var parsed) ? parsed : null;

        await revokedTokenRepo.AddAsync(new RevokedToken(jti, "access_token", client.Client?.Id, userId, expiresAt), ct).ConfigureAwait(false);
        return true;
    }
}
