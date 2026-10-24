using DgDevelopment.Identity.Client.Blazor;
using DgDevelopment.Identity.Client.Core;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace DgDevelopment.Identity.IdentityPlatform;

public sealed class ServerIdentityAuthStateProvider(
    IdentityClient client,
    ITokenStore tokenStore,
    ISessionMarkerService markerService,
    SessionEventClient sessionEventClient,
    OidcOptions options,
    IHttpContextAccessor httpContextAccessor)
    : IdentityAuthStateProvider(client, tokenStore, markerService, sessionEventClient, options)
{
    // HttpContext.User is populated by SessionMarkerAuthenticationHandler (see Program.cs) from the
    // same identity_marker cookie this used to hand-decode directly - reusing it here instead of
    // decoding twice keeps this and the ASP.NET Core SSR-level [Authorize] check (which only ever
    // sees HttpContext.User, never this provider) looking at the exact same identity.
    //
    // Only available for the initial static/SSR request though - an interactive Server circuit
    // keeps running after that request ends, by which point HttpContext is null (a different DI
    // scope than the prerender request). This used to fall back to the base class's OAuth-token
    // path (sessionStorage via JS interop) there, but that path can legitimately fail to find/
    // refresh tokens on a fresh circuit and then calls ClearTokensAndReturnAnonymousAsync, which
    // clears the identity_marker cookie too - wiping out the very cookie the SSR layer just
    // authenticated with and forcing an immediate re-login, which looped forever once [Authorize]
    // started enforcing on every request. Reading the same cookie via JS interop instead keeps one
    // single source of truth (the cookie) for both the prerendered and the live-circuit cases.
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
            return new AuthenticationState(httpContext.User);

        if (SessionMarkerService.Decode(await markerService.GetAsync().ConfigureAwait(false)) is { } marker)
        {
            var userInfo = new UserInfo { Sub = marker.Sub, Name = marker.Name, Email = marker.Email };
            return new AuthenticationState(BuildPrincipal(userInfo));
        }

        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }
}
