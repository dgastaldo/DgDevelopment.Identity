namespace DgDevelopment.Identity.IntegrationTests;

using System.Net.Http.Json;
using System.Text.Json;

/// <summary>
/// Regression guards for pipeline bugs found while manually verifying PR #38/#40 with curl, and
/// while building PR-F's tenant-aware introspection: CORS middleware registered after
/// UseAuthentication/UseAuthorization (dropped CORS headers on authenticated cross-origin
/// responses); the default JwtBearer MapInboundClaims silently renaming the "tid" claim (making
/// ITenantContext.TenantId throw); and the same MapInboundClaims default on JwtService's own
/// internal re-validation (used by /connect/userinfo, introspect, revoke) silently renaming "sub"
/// away from ClaimTypes.NameIdentifier once the identically-named ASP.NET Core pipeline fix was
/// applied there too. All were server-side and invisible to the service-level unit tests, which is
/// exactly why this project exists.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class CorsAndAuthPipelineTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task CrossOriginAuthenticatedRequestReturnsAccessControlAllowOriginHeader()
    {
        using var client = fixture.CreateAuthenticatedClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me/tenants");
        request.Headers.Add("Origin", "https://localhost");

        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"),
            "Expected Access-Control-Allow-Origin on an authenticated cross-origin response - CORS middleware order regressed.");
    }

    [Fact]
    public async Task MeTenantsResolvesTheTenantFromTheTidClaim()
    {
        using var client = fixture.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/me/tenants");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(fixture.DefaultTenantId, body.GetProperty("activeTenantId").GetGuid());
        Assert.True(body.GetProperty("isGlobalAdministrator").GetBoolean());
    }

    [Fact]
    public async Task UserinfoResolvesTheSubjectAfterJwtServicesOwnInternalRevalidation()
    {
        using var client = fixture.CreateAuthenticatedClient();

        var response = await client.GetAsync("/connect/userinfo");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(fixture.SuperAdminUserId, body.GetProperty("sub").GetGuid());
    }
}
