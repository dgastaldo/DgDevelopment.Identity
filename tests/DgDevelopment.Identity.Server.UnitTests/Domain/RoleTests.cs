namespace DgDevelopment.Identity.Server.UnitTests.Domain;

using DgDevelopment.Identity.Domain.Entities;
using Xunit;

public sealed class RoleTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid PlatformId = Guid.NewGuid();

    [Fact]
    public void ConstructorSetsFields()
    {
        var role = new Role(TenantId, PlatformId, "Admin", "Admin role");

        Assert.False(string.IsNullOrWhiteSpace(role.Id.ToString()));
        Assert.Equal("Admin", role.Name);
        Assert.Equal("Admin role", role.Description);
        Assert.Empty(role.Permissions);
    }

    [Fact]
    public void AddPermissionAddsOnceAndRemovePermissionClears()
    {
        var role = new Role(TenantId, PlatformId, "Admin", "desc");
        var permission = new Permission(TenantId, PlatformId, "user:read", "desc", "User");

        role.AddPermission(permission);
        role.AddPermission(permission);

        Assert.Single(role.Permissions);
        Assert.Equal(permission.Id, Assert.Single(role.Permissions).PermissionId);

        role.RemovePermission(permission.Id);

        Assert.Empty(role.Permissions);
    }

    [Fact]
    public void AddPermissionStoresScope()
    {
        var role = new Role(TenantId, PlatformId, "Admin", "desc");
        var permission = new Permission(TenantId, PlatformId, "user:read", "desc", "User");

        role.AddPermission(permission, scopeType: "Platform", scopeValue: "app-1");

        var userPermission = Assert.Single(role.Permissions);
        Assert.Equal("Platform", userPermission.ScopeType);
        Assert.Equal("app-1", userPermission.ScopeValue);
    }
}
