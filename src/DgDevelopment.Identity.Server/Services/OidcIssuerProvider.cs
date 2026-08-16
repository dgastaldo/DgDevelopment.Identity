namespace DgDevelopment.Identity.Server.Services;

using DgDevelopment.Identity.OAuth.Services;
using Microsoft.AspNetCore.Http;

public sealed class OidcIssuerProvider(IHttpContextAccessor httpContextAccessor) : IOidcIssuerProvider
{
    public Uri GetIssuer()
    {
        var request = httpContextAccessor.HttpContext?.Request
            ?? throw new InvalidOperationException("No active HTTP request context.");

        return new Uri($"{request.Scheme}://{request.Host}");
    }
}