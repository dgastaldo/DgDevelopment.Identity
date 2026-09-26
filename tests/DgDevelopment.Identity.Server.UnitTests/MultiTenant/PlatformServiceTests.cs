namespace DgDevelopment.Identity.Server.UnitTests.MultiTenant;

using DgDevelopment.Identity.Application.Platforms;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Xunit;

public sealed class PlatformServiceTests : IClassFixture<DatabaseFixture<PlatformServiceTests>>
{
    private readonly DatabaseFixture<PlatformServiceTests> _fixture;

    public PlatformServiceTests(DatabaseFixture<PlatformServiceTests> fixture)
    {
        _fixture = fixture;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static PlatformService CreateService(IdentityDbContext context)
        => new(new PlatformRepository(context));

    private static async Task<Tenant> CreateTenantAsync(IdentityDbContext context, string name)
    {
        var tenant = new Tenant(name, Unique(name.ToUpperInvariant()));
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
        return tenant;
    }

    private static async Task<Platform> CreatePlatformAsync(IdentityDbContext context, Guid tenantId, string name = "Platform")
    {
        var platform = new Platform(tenantId, Unique(name), "description", PermissionMode.AuthOnly);
        context.Platforms.Add(platform);
        await context.SaveChangesAsync();
        return platform;
    }

    [Fact]
    public async Task GetAsyncReturnsNullForPlatformInDifferentTenant()
    {
        await using var context = _fixture.CreateContext();
        var homeTenant = await CreateTenantAsync(context, "PlatformGetHome");
        var otherTenant = await CreateTenantAsync(context, "PlatformGetOther");
        var platform = await CreatePlatformAsync(context, homeTenant.Id);
        var service = CreateService(context);

        var result = await service.GetAsync(platform.Id, otherTenant.Id, allTenants: false);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPagedAsyncOnlyReturnsPlatformsOfTheActiveTenantUnlessAllTenants()
    {
        await using var context = _fixture.CreateContext();
        var tenantA = await CreateTenantAsync(context, "PlatformPagedA");
        var tenantB = await CreateTenantAsync(context, "PlatformPagedB");
        var platformA = await CreatePlatformAsync(context, tenantA.Id);
        var platformB = await CreatePlatformAsync(context, tenantB.Id);
        var service = CreateService(context);

        var scopedResult = await service.GetPagedAsync(null, 1, 100, tenantA.Id, allTenants: false);
        var globalResult = await service.GetPagedAsync(null, 1, 1000, tenantA.Id, allTenants: true);

        Assert.Contains(scopedResult.Items, p => p.Id == platformA.Id);
        Assert.DoesNotContain(scopedResult.Items, p => p.Id == platformB.Id);
        Assert.Contains(globalResult.Items, p => p.Id == platformA.Id);
        Assert.Contains(globalResult.Items, p => p.Id == platformB.Id);
    }

    [Fact]
    public async Task UpdateAsyncThrowsWhenPlatformBelongsToDifferentTenant()
    {
        await using var context = _fixture.CreateContext();
        var tenantA = await CreateTenantAsync(context, "PlatformUpdateA");
        var tenantB = await CreateTenantAsync(context, "PlatformUpdateB");
        var platform = await CreatePlatformAsync(context, tenantB.Id);
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(platform.Id, "New name", "New description", PermissionMode.IdentityManaged, tenantA.Id, allTenants: false));

        Assert.Equal("The platform does not belong to the active tenant.", exception.Message);
    }

    [Fact]
    public async Task UpdateAsyncPersistsChangesForPlatformInSameTenant()
    {
        Guid platformId;
        Guid tenantId;
        await using (var setupContext = _fixture.CreateContext())
        {
            var tenant = await CreateTenantAsync(setupContext, "PlatformUpdateSame");
            var platform = await CreatePlatformAsync(setupContext, tenant.Id);
            tenantId = tenant.Id;
            platformId = platform.Id;
        }

        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        await service.UpdateAsync(platformId, "Renamed", "New description", PermissionMode.IdentityManaged, tenantId, allTenants: false);

        var stored = await new PlatformRepository(context).GetByIdAsync(platformId);
        Assert.NotNull(stored);
        Assert.Equal("Renamed", stored.Name);
        Assert.Equal("New description", stored.Description);
        Assert.Equal(PermissionMode.IdentityManaged, stored.PermissionMode);
    }

    [Fact]
    public async Task DeleteAsyncThrowsWhenPlatformBelongsToDifferentTenant()
    {
        await using var context = _fixture.CreateContext();
        var tenantA = await CreateTenantAsync(context, "PlatformDeleteA");
        var tenantB = await CreateTenantAsync(context, "PlatformDeleteB");
        var platform = await CreatePlatformAsync(context, tenantB.Id);
        var service = CreateService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteAsync(platform.Id, tenantA.Id, allTenants: false));
    }
}
