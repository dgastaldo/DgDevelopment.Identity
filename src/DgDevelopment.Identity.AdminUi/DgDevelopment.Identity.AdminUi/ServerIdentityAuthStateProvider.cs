using DgDevelopment.Identity.Client.Blazor;
using DgDevelopment.Identity.Client.Core;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace DgDevelopment.Identity.AdminUi;

public sealed class ServerIdentityAuthStateProvider(
    IdentityClient client,
    ITokenStore tokenStore,
    ISessionMarkerService markerService,
    IHttpContextAccessor httpContextAccessor)
    : IdentityAuthStateProvider(client, tokenStore, markerService)
{
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext is not null)
        {
            if (SessionMarkerService.Decode(httpContext.Request.Cookies[SessionMarkerService.CookieName]) is { } marker)
            {
                var userInfo = new UserInfo
                {
                    Sub = marker.Sub,
                    Name = marker.Name,
                    Email = marker.Email
                };

                return new AuthenticationState(BuildPrincipal(userInfo));
            }

            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        return await base.GetAuthenticationStateAsync().ConfigureAwait(false);
    }
}