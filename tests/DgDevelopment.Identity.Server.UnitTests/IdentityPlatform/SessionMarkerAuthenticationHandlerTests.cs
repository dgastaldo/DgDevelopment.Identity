namespace DgDevelopment.Identity.Server.UnitTests.IdentityPlatform;

using System.Security.Claims;
using System.Text.Encodings.Web;
using DgDevelopment.Identity.Client.Blazor;
using DgDevelopment.Identity.IdentityPlatform;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class SessionMarkerAuthenticationHandlerTests
{
    // IOptionsMonitor<T> has no trivial built-in fake - this is the standard minimal stand-in for
    // handing a single, never-reloaded options instance to an AuthenticationHandler<T> under test.
    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private static async Task<SessionMarkerAuthenticationHandler> CreateHandlerAsync(HttpContext context)
    {
        var handler = new SessionMarkerAuthenticationHandler(
            new StaticOptionsMonitor<AuthenticationSchemeOptions>(new AuthenticationSchemeOptions()),
            NullLoggerFactory.Instance,
            UrlEncoder.Default);
        await handler.InitializeAsync(new AuthenticationScheme("Test", "Test", typeof(SessionMarkerAuthenticationHandler)), context);
        return handler;
    }

    private static void SetMarkerCookie(HttpContext context, SessionMarkerDto marker)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(marker);
        context.Request.Headers.Append("Cookie", $"{SessionMarkerService.CookieName}={Uri.EscapeDataString(json)}");
    }

    [Fact]
    public async Task HandleAuthenticateAsyncSucceedsWhenTheMarkerCookieIsPresent()
    {
        var context = new DefaultHttpContext();
        SetMarkerCookie(context, new SessionMarkerDto("user-sub-1", "Jane Doe", "jane@example.com"));
        var handler = await CreateHandlerAsync(context);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Equal("user-sub-1", result.Principal!.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("Jane Doe", result.Principal!.FindFirstValue(ClaimTypes.Name));
        Assert.Equal("jane@example.com", result.Principal!.FindFirstValue(ClaimTypes.Email));
    }

    [Fact]
    public async Task HandleAuthenticateAsyncOmitsOptionalClaimsWhenNameAndEmailAreAbsent()
    {
        var context = new DefaultHttpContext();
        SetMarkerCookie(context, new SessionMarkerDto("user-sub-2", null, null));
        var handler = await CreateHandlerAsync(context);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Equal("user-sub-2", result.Principal!.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Null(result.Principal!.FindFirstValue(ClaimTypes.Name));
        Assert.Null(result.Principal!.FindFirstValue(ClaimTypes.Email));
    }

    [Fact]
    public async Task HandleAuthenticateAsyncReturnsNoResultWithoutAMarkerCookie()
    {
        var context = new DefaultHttpContext();
        var handler = await CreateHandlerAsync(context);

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.Null(result.Failure);
    }

    [Fact]
    public async Task HandleChallengeAsyncRedirectsToLogin()
    {
        var context = new DefaultHttpContext();
        var handler = await CreateHandlerAsync(context);

        await handler.ChallengeAsync();

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/login", context.Response.Headers.Location);
    }
}
