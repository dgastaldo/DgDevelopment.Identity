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
    // sees HttpContext.User, never this provider) looking at the exact same identity. Only available
    // for the initial static/SSR request though - an interactive Server circuit keeps running after
    // that request ends, by which point HttpContext is null, so it falls back to the base
    // implementation (real browser JS interop over the live SignalR connection) exactly as before.
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
            return new AuthenticationState(httpContext.User);

        return await base.GetAuthenticationStateAsync().ConfigureAwait(false);
    }
}
