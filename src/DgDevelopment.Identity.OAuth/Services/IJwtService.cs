namespace DgDevelopment.Identity.OAuth.Services;

using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

public interface IJwtService
{
    Task<string> CreateIdTokenAsync(IdTokenRequest request, CancellationToken ct = default);
    Task<string> CreateAccessTokenAsync(AccessTokenRequest request, CancellationToken ct = default);
    Task<ClaimsPrincipal> ValidateTokenAsync(string token, TokenValidationParameters parameters, CancellationToken ct = default);
}
