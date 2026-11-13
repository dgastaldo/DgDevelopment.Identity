using System.Security.Claims;
using System.Text.Encodings.Web;
using DgDevelopment.Identity.Client.Blazor;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DgDevelopment.Identity.IdentityPlatform;

// Bridges this app's hand-rolled session cookie (identity_marker, written client-side by
// SessionMarkerService - see its own comments) into ASP.NET Core's own authentication system.
// Without this, HttpContext.User stays anonymous on every request no matter what the marker cookie
// says, because nothing ever calls HttpContext.SignInAsync for a real ASP.NET Core auth scheme -
// Blazor's own AuthorizeRouteView (via ServerIdentityAuthStateProvider) saw the user as logged in,
// but the ASP.NET Core AuthorizationMiddleware that [Authorize] also wires up at the SSR endpoint
// level only ever saw anonymous, so every request - authenticated or not - got redirected to
// /login, which looped forever once login succeeded and came back to the same unauthenticated
// endpoint check.
public sealed class SessionMarkerAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var marker = SessionMarkerService.Decode(Request.Cookies[SessionMarkerService.CookieName]);
        if (marker is null)
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, marker.Sub) };
        if (!string.IsNullOrEmpty(marker.Name))
            claims.Add(new Claim(ClaimTypes.Name, marker.Name));
        if (!string.IsNullOrEmpty(marker.Email))
            claims.Add(new Claim(ClaimTypes.Email, marker.Email));

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Redirect("/login");
        return Task.CompletedTask;
    }
}
