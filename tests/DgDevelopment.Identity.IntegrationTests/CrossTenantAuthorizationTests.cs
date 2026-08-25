namespace DgDevelopment.Identity.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

[Collection(IntegrationCollection.Name)]
public sealed class CrossTenantAuthorizationTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task AssigningARoleFromAnotherTenantToAGroupReturns403()
    {
        var (_, otherTenantRoleId) = await fixture.EnsureSecondTenantAsync();
        using var client = fixture.CreateAuthenticatedClient();

        var group = await client.PostAsJsonAsync("/api/v1/groups", new { name = "Cross-Tenant Test Group", description = "for the 403 check" });
        group.EnsureSuccessStatusCode();
        var groupId = (await group.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var assign = await client.PutAsJsonAsync($"/api/v1/groups/{groupId}/roles/{otherTenantRoleId}", new { });

        Assert.Equal(HttpStatusCode.Forbidden, assign.StatusCode);
        var problem = await assign.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("The role does not belong to the group's tenant.", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task SettingAGroupsParentToItselfReturns403Cycle()
    {
        using var client = fixture.CreateAuthenticatedClient();

        var group = await client.PostAsJsonAsync("/api/v1/groups", new { name = "Cycle Test Group", description = "for the cycle check" });
        group.EnsureSuccessStatusCode();
        var groupId = (await group.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var setParent = await client.PutAsJsonAsync($"/api/v1/groups/{groupId}/parent", new { parentGroupId = groupId });

        Assert.Equal(HttpStatusCode.Forbidden, setParent.StatusCode);
        var problem = await setParent.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Setting this parent would create a group hierarchy cycle.", problem.GetProperty("detail").GetString());
    }
}
