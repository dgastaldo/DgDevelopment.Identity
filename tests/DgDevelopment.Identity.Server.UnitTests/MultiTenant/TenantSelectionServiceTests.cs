namespace DgDevelopment.Identity.Server.UnitTests.MultiTenant;

using DgDevelopment.Identity.Application.Authorization;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Xunit;

public sealed class TenantSelectionServiceTests : IClassFixture<DatabaseFixture<TenantSelectionServiceTests>>
{
    private readonly DatabaseFixture<TenantSelectionServiceTests> _fixture;

    public TenantSelectionServiceTests(DatabaseFixture<TenantSelectionServiceTests> fixture)
    {
        _fixture = fixture;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static async Task<User> CreateUserAsync(IdentityDbContext context)
    {
        var user = new User(Unique("user"), "hash", EmailAddress.FromString($"{Guid.NewGuid():N}@example.com"));
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<Tenant> CreateTenantAsync(IdentityDbContext context, string slug)
    {
        var tenant = new Tenant(slug, slug);
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
        return tenant;
    }

    [Fact]
    public async Task ResolveAsyncAutoSelectsTheOnlyMembership()
    {
        await using var context = _fixture.CreateContext();
        var tenant = await CreateTenantAsync(context, Unique("solo"));
        var user = await CreateUserAsync(context);
        context.TenantMemberships.Add(new TenantMembership(tenant.Id, user.Id));
        await context.SaveChangesAsync();

        var service = new TenantSelectionService(new TenantRepository(context));
        var result = await service.ResolveAsync(user.Id, null);

        Assert.False(result.Denied);
        Assert.Equal(tenant.Id, result.TenantId);
    }

    [Fact]
    public async Task ResolveAsyncNeedsSelectionForMultipleMemberships()
    {
        await using var context = _fixture.CreateContext();
        var tenantA = await CreateTenantAsync(context, Unique("multi-a"));
        var tenantB = await CreateTenantAsync(context, Unique("multi-b"));
        var user = await CreateUserAsync(context);
        context.TenantMemberships.Add(new TenantMembership(tenantA.Id, user.Id));
        context.TenantMemberships.Add(new TenantMembership(tenantB.Id, user.Id));
        await context.SaveChangesAsync();

        var service = new TenantSelectionService(new TenantRepository(context));
        var result = await service.ResolveAsync(user.Id, null);

        Assert.False(result.Denied);
        Assert.Null(result.TenantId);
        Assert.Equal(2, result.Choices.Count);
    }

    [Fact]
    public async Task ResolveAsyncSelectsTheRequestedTenantBySlug()
    {
        await using var context = _fixture.CreateContext();
        var tenantA = await CreateTenantAsync(context, Unique("req-a"));
        var tenantB = await CreateTenantAsync(context, Unique("req-b"));
        var user = await CreateUserAsync(context);
        context.TenantMemberships.Add(new TenantMembership(tenantA.Id, user.Id));
        context.TenantMemberships.Add(new TenantMembership(tenantB.Id, user.Id));
        await context.SaveChangesAsync();

        var service = new TenantSelectionService(new TenantRepository(context));
        var result = await service.ResolveAsync(user.Id, tenantB.Slug);

        Assert.False(result.Denied);
        Assert.Equal(tenantB.Id, result.TenantId);
    }

    [Fact]
    public async Task ResolveAsyncDeniesARequestedTenantTheUserDoesNotBelongTo()
    {
        await using var context = _fixture.CreateContext();
        var tenant = await CreateTenantAsync(context, Unique("owned"));
        var foreignTenant = await CreateTenantAsync(context, Unique("foreign"));
        var user = await CreateUserAsync(context);
        context.TenantMemberships.Add(new TenantMembership(tenant.Id, user.Id));
        await context.SaveChangesAsync();

        var service = new TenantSelectionService(new TenantRepository(context));
        var result = await service.ResolveAsync(user.Id, foreignTenant.Slug);

        Assert.True(result.Denied);
        Assert.Null(result.TenantId);
    }

    [Fact]
    public async Task ResolveAsyncDeniesAUserWithNoMemberships()
    {
        await using var context = _fixture.CreateContext();
        var user = await CreateUserAsync(context);

        var service = new TenantSelectionService(new TenantRepository(context));
        var result = await service.ResolveAsync(user.Id, null);

        Assert.True(result.Denied);
    }
}
