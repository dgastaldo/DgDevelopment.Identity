namespace DgDevelopment.Identity.OAuth.Services;

using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Domain.Repositories;
using Microsoft.IdentityModel.Tokens;

public sealed class TokenIntrospectionService(
    IRefreshTokenRepository refreshTokenRepo,
    IRevokedTokenRepository revokedTokenRepo,
    IClientRepository clientRepo,
    IJwtService jwtService,
    IKeyMaterialService keyMaterial,
    IOidcIssuerProvider issuerProvider) : ITokenIntrospectionService
{
    public async Task<IntrospectionResponse> IntrospectAsync(string token, string? tokenTypeHint = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Token is missing.");

        if (tokenTypeHint == "access_token")
            return await IntrospectAccessTokenAsync(token, ct).ConfigureAwait(false);

        if (tokenTypeHint == "refresh_token")
            return await IntrospectRefreshTokenAsync(token, ct).ConfigureAwait(false);

        var refresh = await IntrospectRefreshTokenAsync(token, ct).ConfigureAwait(false);
        if (refresh.Active)
            return refresh;

        return await IntrospectAccessTokenAsync(token, ct).ConfigureAwait(false);
    }

    private async Task<IntrospectionResponse> IntrospectRefreshTokenAsync(string token, CancellationToken ct)
    {
        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var stored = await refreshTokenRepo.GetByTokenHashAsync(tokenHash, ct).ConfigureAwait(false);
        if (stored is null)
            return new(false);

        var client = await clientRepo.GetByIdAsync(stored.ClientId, ct).ConfigureAwait(false);
        return new(
            Active: !stored.IsRevoked && !stored.IsExpired(),
            Scope: stored.Scopes,
            ClientId: client?.ClientId.ToString(),
            Sub: stored.UserId.ToString(null, CultureInfo.InvariantCulture),
            TokenType: "refresh_token",
            Exp: new DateTimeOffset(stored.ExpiresAt).ToUnixTimeSeconds(),
            Iat: new DateTimeOffset(stored.CreatedAt).ToUnixTimeSeconds(),
            Tid: stored.TenantId.ToString(null, CultureInfo.InvariantCulture));
    }

    private async Task<IntrospectionResponse> IntrospectAccessTokenAsync(string token, CancellationToken ct)
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
            return new(false);
        }

        var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        if (jti is not null)
        {
            var jtiHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(jti)));
            var revoked = await revokedTokenRepo.ExistsAsync(jtiHash, ct).ConfigureAwait(false);
            if (revoked)
                return new(false);
        }

        var expClaim = principal.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
        var iatClaim = principal.FindFirst(JwtRegisteredClaimNames.Iat)?.Value;
        long? exp = long.TryParse(expClaim, NumberStyles.None, CultureInfo.InvariantCulture, out var expValue) ? expValue : null;
        long? iat = long.TryParse(iatClaim, NumberStyles.None, CultureInfo.InvariantCulture, out var iatValue) ? iatValue : null;

        var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var username = principal.FindFirst("name")?.Value
            ?? principal.FindFirst(ClaimTypes.Name)?.Value;

        var active = exp is null || exp > EpochTime.GetIntDate(DateTime.UtcNow);

        return new(
            Active: active,
            Scope: principal.FindFirst("scope")?.Value,
            ClientId: principal.FindFirst("client_id")?.Value,
            Sub: sub,
            Username: username,
            TokenType: "Bearer",
            Aud: principal.FindFirst(JwtRegisteredClaimNames.Aud)?.Value,
            Iss: principal.FindFirst(JwtRegisteredClaimNames.Iss)?.Value,
            Exp: exp,
            Iat: iat,
            Jti: jti,
            Permissions: principal.FindAll("permission").Select(c => c.Value).ToArray(),
            Tid: principal.FindFirst("tid")?.Value);
    }
}
