namespace DgDevelopment.Identity.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

[Collection(IntegrationCollection.Name)]
public sealed class AdminApiCrudTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task GetUsersReturnsTheSeededSuperAdmin()
    {
        using var client = fixture.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/users");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, u => u.GetProperty("username").GetString() == IntegrationTestConstants.SuperAdminUserName);
    }

    [Fact]
    public async Task GetPermissionsReturnsTheSeededCatalog()
    {
        using var client = fixture.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/permissions?pageSize=100");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, p => p.GetProperty("name").GetString() == "identity-platform.role.read");
    }

    [Fact]
    public async Task RoleCrudCreateUpdateDeleteRoundTripsOverRealHttp()
    {
        using var client = fixture.CreateAuthenticatedClient();

        var create = await client.PostAsJsonAsync("/api/v1/roles", new { name = "CRUD Role", description = "created by integration test" });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var roleId = created.GetProperty("id").GetGuid();

        var update = await client.PutAsJsonAsync($"/api/v1/roles/{roleId}", new { name = "CRUD Role Renamed", description = "updated" });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var get = await client.GetAsync($"/api/v1/roles/{roleId}");
        get.EnsureSuccessStatusCode();
        var fetched = await get.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CRUD Role Renamed", fetched.GetProperty("name").GetString());

        var delete = await client.DeleteAsync($"/api/v1/roles/{roleId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var getAfterDelete = await client.GetAsync($"/api/v1/roles/{roleId}");
        Assert.Equal(HttpStatusCode.NotFound, getAfterDelete.StatusCode);
    }

    [Fact]
    public async Task AssignAndRemoveRoleOnGroupPersistsOverRealHttp()
    {
        // Regression test, at the HTTP-pipeline level, for the DbUpdateConcurrencyException /
        // silent-removal bug fixed in PR #40 (RoleRepository/GroupRepository/UserRepository
        // UpdateAsync mismarking client-generated-key child rows).
        using var client = fixture.CreateAuthenticatedClient();

        var role = await client.PostAsJsonAsync("/api/v1/roles", new { name = "Assignable Role", description = "for group assignment" });
        role.EnsureSuccessStatusCode();
        var roleId = (await role.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var group = await client.PostAsJsonAsync("/api/v1/groups", new { name = "Assignable Group", description = "for role assignment" });
        group.EnsureSuccessStatusCode();
        var groupId = (await group.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var assign = await client.PutAsJsonAsync($"/api/v1/groups/{groupId}/roles/{roleId}", new { });
        Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);

        var afterAssign = await client.GetAsync($"/api/v1/groups/{groupId}");
        var afterAssignBody = await afterAssign.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(afterAssignBody.GetProperty("roles").EnumerateArray(), r => r.GetProperty("roleId").GetGuid() == roleId);

        var remove = await client.DeleteAsync($"/api/v1/groups/{groupId}/roles/{roleId}");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);

        var afterRemove = await client.GetAsync($"/api/v1/groups/{groupId}");
        var afterRemoveBody = await afterRemove.Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain(afterRemoveBody.GetProperty("roles").EnumerateArray(), r => r.GetProperty("roleId").GetGuid() == roleId);
    }

    [Fact]
    public async Task CreatingARoleWritesATenantScopedAuditLogEntryVisibleOverRealHttp()
    {
        using var client = fixture.CreateAuthenticatedClient();

        var role = await client.PostAsJsonAsync("/api/v1/roles", new { name = "Audited Role", description = "for audit log test" });
        role.EnsureSuccessStatusCode();
        var roleId = (await role.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var audit = await client.GetAsync($"/api/v1/audit?action=role.create&targetId={roleId}");
        audit.EnsureSuccessStatusCode();

        var body = await audit.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();
        var entry = Assert.Single(items);
        Assert.Equal(fixture.DefaultTenantId, entry.GetProperty("tenantId").GetGuid());
        Assert.Equal(roleId.ToString(), entry.GetProperty("targetId").GetString());
    }
}
