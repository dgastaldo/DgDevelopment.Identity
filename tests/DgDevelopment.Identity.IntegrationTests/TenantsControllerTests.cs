namespace DgDevelopment.Identity.IntegrationTests;

using System.Net;
using System.Net.Http.Json;

[Collection(IntegrationCollection.Name)]
public sealed class TenantsControllerTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task PlatformTenantSuperAdminCanListAndCreateTenants()
    {
        using var client = fixture.CreateAuthenticatedClient();

        var list = await client.GetAsync("/api/v1/tenants");
        list.EnsureSuccessStatusCode();

        var create = await client.PostAsJsonAsync("/api/v1/tenants", new { name = "Provisioning Smoke Test Tenant", slug = "provisioning-smoke-test-tenant" });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
    }

    // Regression test for a real cross-tenant privilege-escalation bug: tenant.read/.create are
    // IsGlobal permissions duplicated into every tenant's own catalog, so a customer tenant's own
    // GlobalAdmin holds them too - RequirePermission alone can't tell the two apart. Without the
    // IsPlatformTenant check in TenantContext.IsGlobalAdministratorAsync, this request would
    // succeed instead of being forbidden.
    [Fact]
    public async Task CustomerTenantGlobalAdminCannotListOrCreateTenants()
    {
        using var client = fixture.CreateAuthenticatedClientForCustomerTenantAsync();

        var list = await client.GetAsync("/api/v1/tenants");
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);

        var create = await client.PostAsJsonAsync("/api/v1/tenants", new { name = "Should Never Exist", slug = "should-never-exist" });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task PlatformTenantSuperAdminCanDeactivateAndReactivateATenant()
    {
        using var client = fixture.CreateAuthenticatedClient();

        var deactivate = await client.PutAsJsonAsync($"/api/v1/tenants/{fixture.CustomerTenantId}/active", new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        var deactivated = await deactivate.Content.ReadFromJsonAsync<TenantAdminResponse>();
        Assert.False(deactivated!.IsActive);

        var reactivate = await client.PutAsJsonAsync($"/api/v1/tenants/{fixture.CustomerTenantId}/active", new { isActive = true });
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);
        var reactivated = await reactivate.Content.ReadFromJsonAsync<TenantAdminResponse>();
        Assert.True(reactivated!.IsActive);
    }

    [Fact]
    public async Task DeactivatingThePlatformTenantItselfIsRejected()
    {
        using var client = fixture.CreateAuthenticatedClient();

        var response = await client.PutAsJsonAsync($"/api/v1/tenants/{fixture.DefaultTenantId}/active", new { isActive = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record TenantAdminResponse(Guid Id, string Name, string Slug, bool IsActive, bool IsPlatformTenant, DateTime CreatedAt);
}
