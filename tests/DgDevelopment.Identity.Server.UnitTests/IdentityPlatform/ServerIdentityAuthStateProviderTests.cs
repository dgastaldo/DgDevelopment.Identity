namespace DgDevelopment.Identity.Server.UnitTests.IdentityPlatform;

using System.Security.Claims;
using DgDevelopment.Identity.Client.Blazor;
using DgDevelopment.Identity.Client.Core;
using DgDevelopment.Identity.IdentityPlatform;
using Microsoft.AspNetCore.Http;
using Xunit;

public sealed class ServerIdentityAuthStateProviderTests
{
    private sealed class FakeTokenStore : ITokenStore
    {
        public Task<TokenResponse?> GetTokensAsync() => throw new NotSupportedException();
        public Task SaveTokensAsync(TokenResponse tokens) => throw new NotSupportedException();
        public Task ClearTokensAsync() => throw new NotSupportedException();
    }

    private sealed class FakeSessionMarkerService(string? raw) : ISessionMarkerService
    {
        public Task SetAsync(UserInfo userInfo, CancellationToken ct = default) => throw new NotSupportedException();
        public Task ClearAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string?> GetAsync(CancellationToken ct = default) => Task.FromResult(raw);
    }

    private static ServerIdentityAuthStateProvider CreateProvider(IHttpContextAccessor accessor, ISessionMarkerService? markerService = null)
        => new(
            new IdentityClient(new HttpClient(), new OidcOptions()),
            new FakeTokenStore(),
            markerService ?? new FakeSessionMarkerService(null),
            new SessionEventClient(),
            new OidcOptions(),
            accessor);

    [Fact]
    public async Task GetAuthenticationStateAsyncUsesHttpContextUserWhenAnHttpContextIsAvailable()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "ssr-user")], "marker");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var provider = CreateProvider(accessor);

        var state = await provider.GetAuthenticationStateAsync();

        Assert.Equal("ssr-user", state.User.FindFirstValue(ClaimTypes.NameIdentifier));
    }

    [Fact]
    public async Task GetAuthenticationStateAsyncFallsBackToTheMarkerCookieWhenNoHttpContextIsAvailable()
    {
        var accessor = new HttpContextAccessor { HttpContext = null };
        var marker = new SessionMarkerDto("circuit-user", "Circuit User", "circuit@example.com");
        var markerService = new FakeSessionMarkerService(System.Text.Json.JsonSerializer.Serialize(marker));
        var provider = CreateProvider(accessor, markerService);

        var state = await provider.GetAuthenticationStateAsync();

        Assert.Equal("circuit-user", state.User.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.True(state.User.Identity!.IsAuthenticated);
    }

    [Fact]
    public async Task GetAuthenticationStateAsyncReturnsAnonymousWhenNeitherSourceHasAUser()
    {
        var accessor = new HttpContextAccessor { HttpContext = null };
        var provider = CreateProvider(accessor);

        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated ?? false);
    }
}
