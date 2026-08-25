namespace DgDevelopment.Identity.Server.UnitTests.MultiTenant;

using DgDevelopment.Identity.Application.Roles;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Xunit;

public sealed class RoleServiceTests : IClassFixture<DatabaseFixture<RoleServiceTests>>
{
    private readonly DatabaseFixture<RoleServiceTests> _fixture;

    public RoleServiceTests(DatabaseFixture<RoleServiceTests> fixture)
    {
        _fixture = fixture;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static RoleService CreateService(IdentityDbContext context)
        => new(new RoleRepository(context), new PermissionRepository(context));

    private static async Task<Tenant> CreateTenantAsync(IdentityDbContext context, string name)
    {
        var tenant = new Tenant(name, Unique(name.ToLowerInvariant()));
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
        return tenant;
    }

    private static async Task<Role> CreateRoleAsync(IdentityDbContext context, Guid tenantId, string name = "Role")
    {
        var role = new Role(tenantId, Unique(name), "description");
        context.Roles.Add(role);
        await context.SaveChangesAsync();
        return role;
    }

    private static async Task<Permission> CreatePermissionAsync(IdentityDbContext context, bool isGlobal = false)
    {
        var permission = new Permission(Unique("identity-platform.test.permission"), "description", "Test", isGlobal);
        context.Permissions.Add(permission);
        await context.SaveChangesAsync();
        return permission;
    }

    [Fact]
    public async Task GetAsyncReturnsRoleForItsOwnTenant()
    {
        await using var context = _fixture.CreateContext();
        var tenant = await CreateTenantAsync(context, "RoleOwn");
        var role = await CreateRoleAsync(context, tenant.Id);
        var service = CreateService(context);

        var result = await service.GetAsync(role.Id, tenant.Id, allTenants: false);

        Assert.NotNull(result);
        Assert.Equal(role.Id, result.Id);
    }

    [Fact]
    public async Task GetAsyncReturnsNullForRoleInDifferentTenant()
    {
        await using var context = _fixture.CreateContext();
        var homeTenant = await CreateTenantAsync(context, "RoleHome");
        var otherTenant = await CreateTenantAsync(context, "RoleOther");
        var role = await CreateRoleAsync(context, homeTenant.Id);
        var service = CreateService(context);

        var result = await service.GetAsync(role.Id, otherTenant.Id, allTenants: false);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsyncThrowsForRoleInDifferentTenant()
    {
        await using var context = _fixture.CreateContext();
        var homeTenant = await CreateTenantAsync(context, "RoleUpdHome");
        var otherTenant = await CreateTenantAsync(context, "RoleUpdOther");
        var role = await CreateRoleAsync(context, homeTenant.Id);
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(role.Id, "New name", "New description", otherTenant.Id, allTenants: false));

        Assert.Equal("The role does not belong to the active tenant.", exception.Message);
    }

    [Fact]
    public async Task AssignPermissionAsyncSucceedsRegardlessOfTenantSincePermissionIsGlobal()
    {
        // Permission has no TenantId by design (global catalog) - assigning one to a role
        // never throws a tenant-mismatch error, unlike assigning a role to a group.
        Guid roleId;
        Guid permissionId;
        Guid tenantId;
        await using (var setupContext = _fixture.CreateContext())
        {
            var tenant = await CreateTenantAsync(setupContext, "RolePermTenant");
            var role = await CreateRoleAsync(setupContext, tenant.Id);
            var permission = await CreatePermissionAsync(setupContext);
            tenantId = tenant.Id;
            roleId = role.Id;
            permissionId = permission.Id;
        }

        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        await service.AssignPermissionAsync(roleId, permissionId, null, null, tenantId, allTenants: false);

        var stored = await new RoleRepository(context).GetByIdAsync(roleId);
        Assert.NotNull(stored);
        Assert.Contains(stored.Permissions, p => p.PermissionId == permissionId);
    }

    [Fact]
    public async Task RemovePermissionAsyncRemovesTheGrant()
    {
        Guid roleId;
        Guid permissionId;
        Guid tenantId;
        await using (var setupContext = _fixture.CreateContext())
        {
            var tenant = await CreateTenantAsync(setupContext, "RolePermRemove");
            var role = await CreateRoleAsync(setupContext, tenant.Id);
            var permission = await CreatePermissionAsync(setupContext);
            role.AddPermission(permission);
            setupContext.Roles.Update(role);
            await setupContext.SaveChangesAsync();
            tenantId = tenant.Id;
            roleId = role.Id;
            permissionId = permission.Id;
        }

        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        await service.RemovePermissionAsync(roleId, permissionId, tenantId, allTenants: false);

        var stored = await new RoleRepository(context).GetByIdAsync(roleId);
        Assert.NotNull(stored);
        Assert.DoesNotContain(stored.Permissions, p => p.PermissionId == permissionId);
    }

    [Fact]
    public async Task GetPagedAsyncOnlyReturnsRolesOfTheActiveTenantUnlessAllTenants()
    {
        await using var context = _fixture.CreateContext();
        var tenantA = await CreateTenantAsync(context, "RolePagedA");
        var tenantB = await CreateTenantAsync(context, "RolePagedB");
        var roleA = await CreateRoleAsync(context, tenantA.Id);
        var roleB = await CreateRoleAsync(context, tenantB.Id);
        var service = CreateService(context);

        var scopedResult = await service.GetPagedAsync(null, 1, 100, tenantA.Id, allTenants: false);
        var globalResult = await service.GetPagedAsync(null, 1, 1000, tenantA.Id, allTenants: true);

        Assert.Contains(scopedResult.Items, r => r.Id == roleA.Id);
        Assert.DoesNotContain(scopedResult.Items, r => r.Id == roleB.Id);
        Assert.Contains(globalResult.Items, r => r.Id == roleA.Id);
        Assert.Contains(globalResult.Items, r => r.Id == roleB.Id);
    }

    [Fact]
    public async Task DeleteAsyncThrowsForRoleInDifferentTenant()
    {
        await using var context = _fixture.CreateContext();
        var homeTenant = await CreateTenantAsync(context, "RoleDelHome");
        var otherTenant = await CreateTenantAsync(context, "RoleDelOther");
        var role = await CreateRoleAsync(context, homeTenant.Id);
        var service = CreateService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteAsync(role.Id, otherTenant.Id, allTenants: false));
    }
}
