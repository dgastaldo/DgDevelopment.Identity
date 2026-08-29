namespace DgDevelopment.Identity.Server.UnitTests.MultiTenant;

using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

public sealed class MultiTenantAuthorizationTests : IClassFixture<DatabaseFixture<MultiTenantAuthorizationTests>>
{
    private readonly DatabaseFixture<MultiTenantAuthorizationTests> _fixture;

    public MultiTenantAuthorizationTests(DatabaseFixture<MultiTenantAuthorizationTests> fixture)
    {
        _fixture = fixture;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static async Task<Tenant> CreateTenantAsync(IdentityDbContext context, string name)
    {
        var tenant = new Tenant(name, Unique(name.ToUpperInvariant()));
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
        return tenant;
    }

    private static async Task<Platform> CreatePlatformAsync(IdentityDbContext context, Guid tenantId)
    {
        var platform = new Platform(tenantId, Unique("Platform"), "description", PermissionMode.AuthOnly);
        context.Platforms.Add(platform);
        await context.SaveChangesAsync();
        return platform;
    }

    private static async Task<Role> CreateRoleWithPermissionsAsync(IdentityDbContext context, Guid tenantId, params string[] permissionNames)
    {
        var platform = await CreatePlatformAsync(context, tenantId);
        var role = new Role(tenantId, platform.Id, Unique("role"), "desc");
        var permissions = await context.Permissions.Where(p => permissionNames.Contains(p.Name)).ToListAsync();
        foreach (var permission in permissions)
            role.AddPermission(permission);
        context.Roles.Add(role);
        await context.SaveChangesAsync();
        return role;
    }

    private static async Task<User> CreateUserAsync(IdentityDbContext context)
    {
        var user = new User(Unique("user"), "hash", EmailAddress.FromString($"{Guid.NewGuid():N}@example.com"));
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task UserWithDifferentRolesInTwoTenantsGetsIsolatedEffectivePermissions()
    {
        await using var context = _fixture.CreateContext();
        var tenantA = await CreateTenantAsync(context, "TenantA");
        var tenantB = await CreateTenantAsync(context, "TenantB");
        var roleA = await CreateRoleWithPermissionsAsync(context, tenantA.Id, "identity-platform.user.read");
        var roleB = await CreateRoleWithPermissionsAsync(context, tenantB.Id, "identity-platform.user.update");

        var user = await CreateUserAsync(context);
        user.AssignRole(roleA);
        user.AssignRole(roleB);
        context.Users.Update(user);
        context.TenantMemberships.Add(new TenantMembership(tenantA.Id, user.Id));
        context.TenantMemberships.Add(new TenantMembership(tenantB.Id, user.Id));
        await context.SaveChangesAsync();

        var repo = new UserAuthorizationRepository(context);
        var permissionsInA = await repo.GetEffectivePermissionsAsync(user.Id, tenantA.Id);
        var permissionsInB = await repo.GetEffectivePermissionsAsync(user.Id, tenantB.Id);

        Assert.Contains(permissionsInA, p => p.Name == "identity-platform.user.read");
        Assert.DoesNotContain(permissionsInA, p => p.Name == "identity-platform.user.update");
        Assert.Contains(permissionsInB, p => p.Name == "identity-platform.user.update");
        Assert.DoesNotContain(permissionsInB, p => p.Name == "identity-platform.user.read");
    }

    [Fact]
    public async Task GlobalPermissionIsRecognizedRegardlessOfActiveTenant()
    {
        await using var context = _fixture.CreateContext();
        var homeTenant = await CreateTenantAsync(context, "Home");
        var otherTenant = await CreateTenantAsync(context, "Other");
        var globalAdminRole = await CreateRoleWithPermissionsAsync(context, homeTenant.Id, "identity-platform.tenant.read");

        var user = await CreateUserAsync(context);
        user.AssignRole(globalAdminRole);
        context.Users.Update(user);
        context.TenantMemberships.Add(new TenantMembership(homeTenant.Id, user.Id));
        await context.SaveChangesAsync();

        var repo = new UserAuthorizationRepository(context);

        var permissionsInOtherTenant = await repo.GetEffectivePermissionsAsync(user.Id, otherTenant.Id);

        Assert.Contains(permissionsInOtherTenant, p => p.Name == "identity-platform.tenant.read");
    }

    [Fact]
    public async Task NonGlobalPermissionDoesNotLeakIntoAnotherTenant()
    {
        await using var context = _fixture.CreateContext();
        var homeTenant = await CreateTenantAsync(context, "Home2");
        var otherTenant = await CreateTenantAsync(context, "Other2");
        var localAdminRole = await CreateRoleWithPermissionsAsync(context, homeTenant.Id, "identity-platform.user.delete");

        var user = await CreateUserAsync(context);
        user.AssignRole(localAdminRole);
        context.Users.Update(user);
        context.TenantMemberships.Add(new TenantMembership(homeTenant.Id, user.Id));
        await context.SaveChangesAsync();

        var repo = new UserAuthorizationRepository(context);
        var permissionsInOtherTenant = await repo.GetEffectivePermissionsAsync(user.Id, otherTenant.Id);

        Assert.DoesNotContain(permissionsInOtherTenant, p => p.Name == "identity-platform.user.delete");
    }

    [Fact]
    public async Task GroupDerivedRoleIsScopedToTheGroupsTenant()
    {
        await using var context = _fixture.CreateContext();
        var tenantA = await CreateTenantAsync(context, "GroupTenantA");
        var tenantB = await CreateTenantAsync(context, "GroupTenantB");
        var role = await CreateRoleWithPermissionsAsync(context, tenantA.Id, "identity-platform.group.read");

        var group = new Group(tenantA.Id, Unique("group"), "desc");
        group.AddRole(role);
        context.Groups.Add(group);

        var user = await CreateUserAsync(context);
        user.AddToGroup(group);
        context.Users.Update(user);
        context.TenantMemberships.Add(new TenantMembership(tenantA.Id, user.Id));
        await context.SaveChangesAsync();

        var repo = new UserAuthorizationRepository(context);
        var permissionsInA = await repo.GetEffectivePermissionsAsync(user.Id, tenantA.Id);
        var permissionsInB = await repo.GetEffectivePermissionsAsync(user.Id, tenantB.Id);

        Assert.Contains(permissionsInA, p => p.Name == "identity-platform.group.read");
        Assert.DoesNotContain(permissionsInB, p => p.Name == "identity-platform.group.read");
    }
}
