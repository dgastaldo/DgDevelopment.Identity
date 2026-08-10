using System.Text.Json;
using DgDevelopment.Identity.Client.Blazor;
using DgDevelopment.Identity.Client.Core;
using Microsoft.AspNetCore.Http;

namespace DgDevelopment.Identity.AdminUi;

public sealed class CookieTokenStore : ITokenStore
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string CookieName = "identity_auth";

    public CookieTokenStore(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<TokenResponse?> GetTokensAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Request.Cookies.TryGetValue(CookieName, out var json) == true && json != null)
        {
            var tokens = JsonSerializer.Deserialize<TokenResponse>(json);
            return Task.FromResult(tokens);
        }

        return Task.FromResult<TokenResponse?>(null);
    }

    public Task SaveTokensAsync(TokenResponse tokens)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var json = JsonSerializer.Serialize(tokens);

        httpContext?.Response.Cookies.Append(CookieName, json, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(1)
        });

        return Task.CompletedTask;
    }

    public Task ClearTokensAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        httpContext?.Response.Cookies.Delete(CookieName);
        return Task.CompletedTask;
    }
}
