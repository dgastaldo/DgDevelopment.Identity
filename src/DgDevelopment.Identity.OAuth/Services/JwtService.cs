namespace DgDevelopment.Identity.OAuth.Services;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

public sealed class JwtService : IJwtService
{
    private readonly IKeyMaterialService _keyMaterial;

    public JwtService(IKeyMaterialService keyMaterial)
    {
        _keyMaterial = keyMaterial;
    }

    public async Task<string> CreateIdTokenAsync(IdTokenRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var credentials = await _keyMaterial.GetSigningCredentialsAsync(ct).ConfigureAwait(false);
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, request.User.Id.ToString()),
            new(JwtRegisteredClaimNames.Iss, "https://identity.dgdevelopment.local"),
            new(JwtRegisteredClaimNames.Aud, request.Client.ClientId),
            new(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(now).ToString()),
            new(JwtRegisteredClaimNames.Exp, EpochTime.GetIntDate(now.AddMinutes(5)).ToString()),
            new(JwtRegisteredClaimNames.AuthTime, EpochTime.GetIntDate(now).ToString()),
            new("sid", request.SessionId)
        };

        if (request.Nonce != null)
            claims.Add(new(JwtRegisteredClaimNames.Nonce, request.Nonce));

        var primaryEmail = request.User.PrimaryEmail;
        if (primaryEmail != null)
        {
            claims.Add(new(JwtRegisteredClaimNames.Email, primaryEmail.Value));
            claims.Add(new("email_verified", "true"));
        }

        claims.Add(new("name", request.User.Username));

        foreach (var method in request.AuthMethods)
            claims.Add(new("amr", method));

        var token = new JwtSecurityToken(
            issuer: "https://identity.dgdevelopment.local",
            audience: request.Client.ClientId,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<string> CreateAccessTokenAsync(AccessTokenRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var credentials = await _keyMaterial.GetSigningCredentialsAsync(ct).ConfigureAwait(false);
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, request.User.Id.ToString()),
            new(JwtRegisteredClaimNames.Iss, "https://identity.dgdevelopment.local"),
            new("client_id", request.Client.ClientId),
            new(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(now).ToString()),
            new(JwtRegisteredClaimNames.Exp, EpochTime.GetIntDate(now.AddSeconds(request.LifetimeSeconds)).ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("scope", string.Join(' ', request.Scopes))
        };

        if (request.Permissions is { Length: > 0 })
            claims.Add(new("permission", string.Join(' ', request.Permissions)));

        var token = new JwtSecurityToken(
            issuer: "https://identity.dgdevelopment.local",
            audience: request.Client.ClientId,
            claims: claims,
            notBefore: now,
            expires: now.AddSeconds(request.LifetimeSeconds),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public Task<ClaimsPrincipal> ValidateTokenAsync(string token, TokenValidationParameters parameters, CancellationToken ct = default)
    {
        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(token, parameters, out _);
        return Task.FromResult(principal);
    }
}
