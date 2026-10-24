namespace DgDevelopment.Identity.Server.UnitTests.MultiTenant;

using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Xunit;

public sealed class UserAuthorizationRepositoryRoleTests : IClassFixture<DatabaseFixture<UserAuthorizationRepositoryRoleTests>>
{
    private readonly DatabaseFixture<UserAuthorizationRepositoryRoleTests> _fixture;

    public UserAuthorizationRepositoryRoleTests(DatabaseFixture<UserAuthorizationRepositoryRoleTests> fixture)
    {
        _fixture = fixture;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static async Task<Tenant> CreateTenantAsync(IdentityDbContext context)
    {
        var tenant = new Tenant(Unique("tenant"), Unique("tenant"));
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

    private static async Task<Role> CreateRoleAsync(IdentityDbContext context, Guid tenantId, Guid platformId, string name)
    {
        var role = new Role(tenantId, platformId, name, "desc");
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
    public async Task HasAnyRoleAsyncReturnsTrueForADirectlyAssignedRole()
    {
        await using var context = _fixture.CreateContext();
        var tenant = await CreateTenantAsync(context);
        var platform = await CreatePlatformAsync(context, tenant.Id);
        var role = await CreateRoleAsync(context, tenant.Id, platform.Id, "GlobalAdmin");
        var user = await CreateUserAsync(context);
        user.AssignRole(role);
        context.Users.Update(user);
        await context.SaveChangesAsync();

        var repo = new UserAuthorizationRepository(context);
        var result = await repo.HasAnyRoleAsync(user.Id, tenant.Id, ["GlobalAdmin", "SuperAdmin"]);

        Assert.True(result);
    }

    [Fact]
    public async Task HasAnyRoleAsyncReturnsTrueForARoleHeldThroughGroupMembership()
    {
        await using var context = _fixture.CreateContext();
        var tenant = await CreateTenantAsync(context);
        var platform = await CreatePlatformAsync(context, tenant.Id);
        var role = await CreateRoleAsync(context, tenant.Id, platform.Id, "SuperAdmin");
        var group = new Group(tenant.Id, Unique("SuperAdmins"), "desc");
        group.AddRole(role);
        context.Groups.Add(group);
        await context.SaveChangesAsync();

        var user = await CreateUserAsync(context);
        user.AddToGroup(group);
        context.Users.Update(user);
        await context.SaveChangesAsync();

        var repo = new UserAuthorizationRepository(context);
        var result = await repo.HasAnyRoleAsync(user.Id, tenant.Id, ["GlobalAdmin", "SuperAdmin"]);

        Assert.True(result);
    }

    [Fact]
    public async Task HasAnyRoleAsyncReturnsFalseForARoleHeldInAnotherTenant()
    {
        await using var context = _fixture.CreateContext();
        var homeTenant = await CreateTenantAsync(context);
        var otherTenant = await CreateTenantAsync(context);
        var platform = await CreatePlatformAsync(context, homeTenant.Id);
        var role = await CreateRoleAsync(context, homeTenant.Id, platform.Id, "GlobalAdmin");
        var user = await CreateUserAsync(context);
        user.AssignRole(role);
        context.Users.Update(user);
        await context.SaveChangesAsync();

        var repo = new UserAuthorizationRepository(context);
        var result = await repo.HasAnyRoleAsync(user.Id, otherTenant.Id, ["GlobalAdmin", "SuperAdmin"]);

        Assert.False(result);
    }

    [Fact]
    public async Task HasAnyRoleAsyncReturnsFalseWhenTheUserHoldsNoMatchingRole()
    {
        await using var context = _fixture.CreateContext();
        var tenant = await CreateTenantAsync(context);
        var platform = await CreatePlatformAsync(context, tenant.Id);
        var role = await CreateRoleAsync(context, tenant.Id, platform.Id, "RegularMember");
        var user = await CreateUserAsync(context);
        user.AssignRole(role);
        context.Users.Update(user);
        await context.SaveChangesAsync();

        var repo = new UserAuthorizationRepository(context);
        var result = await repo.HasAnyRoleAsync(user.Id, tenant.Id, ["GlobalAdmin", "SuperAdmin"]);

        Assert.False(result);
    }
}
