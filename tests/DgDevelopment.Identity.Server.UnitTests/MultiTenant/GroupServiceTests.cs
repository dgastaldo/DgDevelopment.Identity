namespace DgDevelopment.Identity.Server.UnitTests.MultiTenant;

using DgDevelopment.Identity.Application.Groups;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Xunit;

public sealed class GroupServiceTests : IClassFixture<DatabaseFixture<GroupServiceTests>>
{
    private readonly DatabaseFixture<GroupServiceTests> _fixture;

    public GroupServiceTests(DatabaseFixture<GroupServiceTests> fixture)
    {
        _fixture = fixture;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static GroupService CreateService(IdentityDbContext context)
        => new(new GroupRepository(context), new RoleRepository(context));

    private static async Task<Tenant> CreateTenantAsync(IdentityDbContext context, string name)
    {
        var tenant = new Tenant(name, Unique(name.ToLowerInvariant()));
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
        return tenant;
    }

    private static async Task<Group> CreateGroupAsync(IdentityDbContext context, Guid tenantId, string name = "Group", Guid? parentGroupId = null)
    {
        var group = new Group(tenantId, Unique(name), "description", parentGroupId);
        context.Groups.Add(group);
        await context.SaveChangesAsync();
        return group;
    }

    private static async Task<Platform> CreatePlatformAsync(IdentityDbContext context, Guid tenantId)
    {
        var platform = new Platform(tenantId, Unique("Platform"), "description", PermissionMode.AuthOnly);
        context.Platforms.Add(platform);
        await context.SaveChangesAsync();
        return platform;
    }

    private static async Task<Role> CreateRoleAsync(IdentityDbContext context, Guid tenantId, string name = "Role")
    {
        var platform = await CreatePlatformAsync(context, tenantId);
        var role = new Role(tenantId, platform.Id, Unique(name), "description");
        context.Roles.Add(role);
        await context.SaveChangesAsync();
        return role;
    }

    [Fact]
    public async Task AssignRoleAsyncThrowsWhenRoleBelongsToDifferentTenant()
    {
        await using var context = _fixture.CreateContext();
        var tenantA = await CreateTenantAsync(context, "GroupRoleA");
        var tenantB = await CreateTenantAsync(context, "GroupRoleB");
        var group = await CreateGroupAsync(context, tenantA.Id);
        var role = await CreateRoleAsync(context, tenantB.Id);
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AssignRoleAsync(group.Id, role.Id, null, null, tenantA.Id, allTenants: false));

        Assert.Equal("The role does not belong to the group's tenant.", exception.Message);
    }

    [Fact]
    public async Task AssignRoleAsyncSucceedsWhenRoleBelongsToSameTenant()
    {
        Guid groupId;
        Guid roleId;
        Guid tenantId;
        await using (var setupContext = _fixture.CreateContext())
        {
            var tenant = await CreateTenantAsync(setupContext, "GroupRoleSame");
            var group = await CreateGroupAsync(setupContext, tenant.Id);
            var role = await CreateRoleAsync(setupContext, tenant.Id);
            tenantId = tenant.Id;
            groupId = group.Id;
            roleId = role.Id;
        }

        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        await service.AssignRoleAsync(groupId, roleId, null, null, tenantId, allTenants: false);

        var stored = await new GroupRepository(context).GetByIdAsync(groupId);
        Assert.NotNull(stored);
        Assert.Contains(stored.Roles, r => r.RoleId == roleId);
    }

    [Fact]
    public async Task SetParentAsyncThrowsOnDirectCycle()
    {
        await using var context = _fixture.CreateContext();
        var tenant = await CreateTenantAsync(context, "GroupCycleDirect");
        var group = await CreateGroupAsync(context, tenant.Id);
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SetParentAsync(group.Id, group.Id, tenant.Id, allTenants: false));

        Assert.Equal("Setting this parent would create a group hierarchy cycle.", exception.Message);
    }

    [Fact]
    public async Task SetParentAsyncThrowsOnIndirectCycle()
    {
        // Chain: A -> B -> C. Trying to set A's parent to C (making C -> A -> B -> C) must fail.
        await using var context = _fixture.CreateContext();
        var tenant = await CreateTenantAsync(context, "GroupCycleIndirect");
        var groupA = await CreateGroupAsync(context, tenant.Id, "A");
        var groupB = await CreateGroupAsync(context, tenant.Id, "B", groupA.Id);
        var groupC = await CreateGroupAsync(context, tenant.Id, "C", groupB.Id);
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SetParentAsync(groupA.Id, groupC.Id, tenant.Id, allTenants: false));

        Assert.Equal("Setting this parent would create a group hierarchy cycle.", exception.Message);
    }

    [Fact]
    public async Task SetParentAsyncThrowsWhenParentInDifferentTenant()
    {
        await using var context = _fixture.CreateContext();
        var tenantA = await CreateTenantAsync(context, "GroupParentA");
        var tenantB = await CreateTenantAsync(context, "GroupParentB");
        var group = await CreateGroupAsync(context, tenantA.Id);
        var otherTenantParent = await CreateGroupAsync(context, tenantB.Id);
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SetParentAsync(group.Id, otherTenantParent.Id, tenantA.Id, allTenants: false));

        Assert.Equal("The parent group does not belong to the active tenant.", exception.Message);
    }

    [Fact]
    public async Task SetParentAsyncSucceedsForValidParentInSameTenant()
    {
        Guid childId;
        Guid parentId;
        Guid tenantId;
        await using (var setupContext = _fixture.CreateContext())
        {
            var tenant = await CreateTenantAsync(setupContext, "GroupParentValid");
            var parent = await CreateGroupAsync(setupContext, tenant.Id, "Parent");
            var child = await CreateGroupAsync(setupContext, tenant.Id, "Child");
            tenantId = tenant.Id;
            parentId = parent.Id;
            childId = child.Id;
        }

        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        await service.SetParentAsync(childId, parentId, tenantId, allTenants: false);

        var stored = await new GroupRepository(context).GetByIdAsync(childId);
        Assert.NotNull(stored);
        Assert.Equal(parentId, stored.ParentGroupId);
    }

    [Fact]
    public async Task CreateAsyncThrowsWhenParentInDifferentTenant()
    {
        await using var context = _fixture.CreateContext();
        var tenantA = await CreateTenantAsync(context, "GroupCreateA");
        var tenantB = await CreateTenantAsync(context, "GroupCreateB");
        var otherTenantParent = await CreateGroupAsync(context, tenantB.Id);
        var service = CreateService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync("New group", "description", otherTenantParent.Id, tenantA.Id));
    }

    [Fact]
    public async Task GetAsyncReturnsNullForGroupInDifferentTenant()
    {
        await using var context = _fixture.CreateContext();
        var homeTenant = await CreateTenantAsync(context, "GroupGetHome");
        var otherTenant = await CreateTenantAsync(context, "GroupGetOther");
        var group = await CreateGroupAsync(context, homeTenant.Id);
        var service = CreateService(context);

        var result = await service.GetAsync(group.Id, otherTenant.Id, allTenants: false);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPagedAsyncOnlyReturnsGroupsOfTheActiveTenantUnlessAllTenants()
    {
        await using var context = _fixture.CreateContext();
        var tenantA = await CreateTenantAsync(context, "GroupPagedA");
        var tenantB = await CreateTenantAsync(context, "GroupPagedB");
        var groupA = await CreateGroupAsync(context, tenantA.Id);
        var groupB = await CreateGroupAsync(context, tenantB.Id);
        var service = CreateService(context);

        var scopedResult = await service.GetPagedAsync(null, 1, 100, tenantA.Id, allTenants: false);
        var globalResult = await service.GetPagedAsync(null, 1, 1000, tenantA.Id, allTenants: true);

        Assert.Contains(scopedResult.Items, g => g.Id == groupA.Id);
        Assert.DoesNotContain(scopedResult.Items, g => g.Id == groupB.Id);
        Assert.Contains(globalResult.Items, g => g.Id == groupA.Id);
        Assert.Contains(globalResult.Items, g => g.Id == groupB.Id);
    }
}
