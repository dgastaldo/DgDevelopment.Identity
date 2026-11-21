namespace DgDevelopment.Identity.Server.UnitTests.IdentityPlatform;

using DgDevelopment.Identity.IdentityPlatform.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Xunit;

public sealed class SecurityHeadersMiddlewareTests
{
    // OnStarting callbacks (how UseSecurityHeaders sets its headers) only fire when something
    // actually starts writing the response through a real pipeline - a bare DefaultHttpContext
    // never triggers them, so this needs an actual TestServer round-trip rather than invoking the
    // middleware delegate directly.
    private static async Task<HttpResponseMessage> SendThroughMiddlewareAsync()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder => webBuilder
                .UseTestServer()
                .Configure(app =>
                {
                    app.UseSecurityHeaders();
                    app.Run(context => context.Response.WriteAsync("ok"));
                }))
            .StartAsync();

        using var client = host.GetTestClient();
        return await client.GetAsync("/");
    }

    [Fact]
    public async Task SetsContentSecurityPolicyAllowingWasmUnsafeEval()
    {
        using var response = await SendThroughMiddlewareAsync();

        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("default-src 'self'", csp, StringComparison.Ordinal);
        Assert.Contains("script-src 'self' 'wasm-unsafe-eval'", csp, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", csp, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetsTheStandardHardeningHeaders()
    {
        using var response = await SendThroughMiddlewareAsync();

        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("strict-origin-when-cross-origin", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
    }

    [Fact]
    public void UseSecurityHeadersThrowsForANullApplicationBuilder()
    {
        Assert.Throws<ArgumentNullException>(() => ((IApplicationBuilder)null!).UseSecurityHeaders());
    }
}
