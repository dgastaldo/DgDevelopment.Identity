namespace DgDevelopment.Identity.Server.UnitTests.MultiTenant;

using DgDevelopment.Identity.Application.Users;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Xunit;

public sealed class MultiTenantUserServiceTests : IClassFixture<DatabaseFixture<MultiTenantUserServiceTests>>
{
    private readonly DatabaseFixture<MultiTenantUserServiceTests> _fixture;

    public MultiTenantUserServiceTests(DatabaseFixture<MultiTenantUserServiceTests> fixture)
    {
        _fixture = fixture;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static UserService CreateService(IdentityDbContext context)
        => new(
            new UserRepository(context),
            new RoleRepository(context),
            new PermissionRepository(context),
            new GroupRepository(context),
            new ClientRepository(context),
            new TenantRepository(context),
            new FakePasswordHasher());

    private static async Task<Tenant> CreateTenantAsync(IdentityDbContext context, string name)
    {
        var tenant = new Tenant(name, Unique(name.ToLowerInvariant()));
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

    private static async Task<User> CreateUserInTenantAsync(IdentityDbContext context, Guid tenantId)
    {
        var user = new User(Unique("user"), "hash", EmailAddress.FromString($"{Guid.NewGuid():N}@example.com"));
        context.Users.Add(user);
        context.TenantMemberships.Add(new TenantMembership(tenantId, user.Id));
        await context.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task GetAsyncReturnsUserForItsOwnTenant()
    {
        await using var context = _fixture.CreateContext();
        var tenant = await CreateTenantAsync(context, "Own");
        var user = await CreateUserInTenantAsync(context, tenant.Id);
        var service = CreateService(context);

        var result = await service.GetAsync(user.Id, tenant.Id, allTenants: false);

        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
    }

    [Fact]
    public async Task GetAsyncReturnsNullForAnotherTenant()
    {
        await using var context = _fixture.CreateContext();
        var homeTenant = await CreateTenantAsync(context, "GetHome");
        var otherTenant = await CreateTenantAsync(context, "GetOther");
        var user = await CreateUserInTenantAsync(context, homeTenant.Id);
        var service = CreateService(context);

        var result = await service.GetAsync(user.Id, otherTenant.Id, allTenants: false);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsyncWithAllTenantsBypassesMembershipCheck()
    {
        await using var context = _fixture.CreateContext();
        var homeTenant = await CreateTenantAsync(context, "AllHome");
        var otherTenant = await CreateTenantAsync(context, "AllOther");
        var user = await CreateUserInTenantAsync(context, homeTenant.Id);
        var service = CreateService(context);

        var result = await service.GetAsync(user.Id, otherTenant.Id, allTenants: true);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task DeactivateAsyncThrowsForUserOutsideActiveTenant()
    {
        await using var context = _fixture.CreateContext();
        var homeTenant = await CreateTenantAsync(context, "DeactHome");
        var otherTenant = await CreateTenantAsync(context, "DeactOther");
        var user = await CreateUserInTenantAsync(context, homeTenant.Id);
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeactivateAsync(user.Id, otherTenant.Id, allTenants: false));

        Assert.Equal("The user does not belong to the active tenant.", exception.Message);
    }

    [Fact]
    public async Task DeactivateAsyncSucceedsForUserInsideActiveTenant()
    {
        Guid userId;
        Guid tenantId;
        await using (var setupContext = _fixture.CreateContext())
        {
            var tenant = await CreateTenantAsync(setupContext, "DeactOwn");
            var user = await CreateUserInTenantAsync(setupContext, tenant.Id);
            tenantId = tenant.Id;
            userId = user.Id;
        }

        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        await service.DeactivateAsync(userId, tenantId, allTenants: false);

        var stored = await new UserRepository(context).GetByIdAsync(userId);
        Assert.NotNull(stored);
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task GetPagedAsyncOnlyReturnsUsersOfTheActiveTenantUnlessAllTenants()
    {
        await using var context = _fixture.CreateContext();
        var tenantA = await CreateTenantAsync(context, "PagedA");
        var tenantB = await CreateTenantAsync(context, "PagedB");
        var userA = await CreateUserInTenantAsync(context, tenantA.Id);
        var userB = await CreateUserInTenantAsync(context, tenantB.Id);
        var service = CreateService(context);

        var scopedResult = await service.GetPagedAsync(null, 1, 100, tenantA.Id, allTenants: false);
        var globalResult = await service.GetPagedAsync(null, 1, 1000, tenantA.Id, allTenants: true);

        Assert.Contains(scopedResult.Items, u => u.Id == userA.Id);
        Assert.DoesNotContain(scopedResult.Items, u => u.Id == userB.Id);
        Assert.Contains(globalResult.Items, u => u.Id == userA.Id);
        Assert.Contains(globalResult.Items, u => u.Id == userB.Id);
    }

    [Fact]
    public async Task DashboardStatisticsAreIsolatedPerTenant()
    {
        await using var context = _fixture.CreateContext();
        var tenantA = await CreateTenantAsync(context, "DashA");
        var tenantB = await CreateTenantAsync(context, "DashB");
        await CreateUserInTenantAsync(context, tenantA.Id);
        await CreateUserInTenantAsync(context, tenantA.Id);
        await CreateUserInTenantAsync(context, tenantB.Id);
        var repo = new UserRepository(context);

        var statsA = await repo.GetStatisticsAsync(tenantA.Id, allTenants: false);
        var statsB = await repo.GetStatisticsAsync(tenantB.Id, allTenants: false);

        Assert.Equal(2, statsA.Total);
        Assert.Equal(1, statsB.Total);
    }

    [Fact]
    public async Task AssignRoleAsyncPersistsTheAssignment()
    {
        // Regression test: UserRepository.UpdateAsync used to attach the whole User graph via
        // DbSet.Update(), which marks client-generated-key children like UserRole as Modified
        // instead of Added, throwing DbUpdateConcurrencyException for a brand new assignment.
        Guid userId;
        Guid roleId;
        Guid tenantId;
        await using (var setupContext = _fixture.CreateContext())
        {
            var tenant = await CreateTenantAsync(setupContext, "AssignRole");
            var platform = await CreatePlatformAsync(setupContext, tenant.Id);
            var user = await CreateUserInTenantAsync(setupContext, tenant.Id);
            var role = new Role(tenant.Id, platform.Id, Unique("Role"), "description");
            setupContext.Roles.Add(role);
            await setupContext.SaveChangesAsync();
            tenantId = tenant.Id;
            userId = user.Id;
            roleId = role.Id;
        }

        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        await service.AssignRoleAsync(userId, roleId, null, null, tenantId, allTenants: false);

        var stored = await new UserRepository(context).GetByIdAsync(userId);
        Assert.NotNull(stored);
        Assert.Contains(stored.Roles, r => r.RoleId == roleId);
    }

    [Fact]
    public async Task RemoveRoleAsyncPersistsTheRemoval()
    {
        // Regression test: removing a child from a disconnected AsNoTracking graph and calling
        // DbSet.Update() never generated a DELETE, so the removal silently didn't persist.
        Guid userId;
        Guid roleId;
        Guid tenantId;
        await using (var setupContext = _fixture.CreateContext())
        {
            var tenant = await CreateTenantAsync(setupContext, "RemoveRole");
            var platform = await CreatePlatformAsync(setupContext, tenant.Id);
            var user = await CreateUserInTenantAsync(setupContext, tenant.Id);
            var role = new Role(tenant.Id, platform.Id, Unique("Role"), "description");
            setupContext.Roles.Add(role);
            await setupContext.SaveChangesAsync();
            user.AssignRole(role, null, null);
            setupContext.Users.Update(user);
            await setupContext.SaveChangesAsync();
            tenantId = tenant.Id;
            userId = user.Id;
            roleId = role.Id;
        }

        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        await service.RemoveRoleAsync(userId, roleId, tenantId, allTenants: false);

        var stored = await new UserRepository(context).GetByIdAsync(userId);
        Assert.NotNull(stored);
        Assert.DoesNotContain(stored.Roles, r => r.RoleId == roleId);
    }
}
