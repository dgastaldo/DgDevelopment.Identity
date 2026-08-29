namespace DgDevelopment.Identity.IntegrationTests;

using System.Text.RegularExpressions;

[Collection(IntegrationCollection.Name)]
public sealed partial class SecurityHeadersTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task ResponsesCarryTheExpectedSecurityHeaders()
    {
        using var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/account/login");

        Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out var contentTypeOptions));
        Assert.Equal("nosniff", contentTypeOptions!.Single());

        Assert.True(response.Headers.TryGetValues("X-Frame-Options", out var frameOptions));
        Assert.Equal("DENY", frameOptions!.Single());

        Assert.True(response.Headers.TryGetValues("Referrer-Policy", out var referrerPolicy));
        Assert.Equal("strict-origin-when-cross-origin", referrerPolicy!.Single());

        Assert.True(response.Headers.TryGetValues("Content-Security-Policy", out var cspValues));
        var csp = cspValues!.Single();
        Assert.Contains("default-src 'self'", csp, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", csp, StringComparison.Ordinal);
        // img-src needs 'data:' specifically for the TOTP QR code <img src="data:image/png;base64,...">
        // on Account/Mfa and Account/MfaEnroll.
        Assert.Contains("img-src 'self' data:", csp, StringComparison.Ordinal);
        Assert.True(NonceRegex().IsMatch(csp), $"Expected a script-src nonce in: {csp}");
    }

    [Fact]
    public async Task EachResponseGetsAFreshCspNonce()
    {
        using var client = fixture.Factory.CreateClient();

        var first = await client.GetAsync("/account/login");
        var second = await client.GetAsync("/account/login");

        var firstNonce = NonceRegex().Match(first.Headers.GetValues("Content-Security-Policy").Single()).Groups[1].Value;
        var secondNonce = NonceRegex().Match(second.Headers.GetValues("Content-Security-Policy").Single()).Groups[1].Value;

        Assert.NotEqual(firstNonce, secondNonce);
    }

    [GeneratedRegex("'nonce-([^']+)'")]
    private static partial Regex NonceRegex();
}
