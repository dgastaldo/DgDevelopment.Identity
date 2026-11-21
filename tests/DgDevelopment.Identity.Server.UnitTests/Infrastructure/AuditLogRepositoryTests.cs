namespace DgDevelopment.Identity.Server.UnitTests.Infrastructure;

using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Xunit;

public sealed class AuditLogRepositoryTests : IClassFixture<DatabaseFixture<AuditLogRepositoryTests>>
{
    private readonly DatabaseFixture<AuditLogRepositoryTests> _fixture;

    public AuditLogRepositoryTests(DatabaseFixture<AuditLogRepositoryTests> fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AddAsyncPersistsTheEntry()
    {
        var tenantId = await _fixture.GetSeededTenantIdAsync();
        await using var context = _fixture.CreateContext();
        var repo = new AuditLogRepository(context);
        var entry = new AuditLog(tenantId, "user.created", AuditOutcome.Success);

        await repo.AddAsync(entry);

        var count = await repo.CountAsync(new AuditLogFilter(tenantId, AllTenants: false));
        Assert.True(count >= 1);
    }

    [Fact]
    public async Task GetPagedAsyncFiltersByActorTypeActorIdAndAction()
    {
        var tenantId = await _fixture.GetSeededTenantIdAsync();
        var actorId = Guid.NewGuid();
        await using var context = _fixture.CreateContext();
        var repo = new AuditLogRepository(context);

        await repo.AddAsync(new AuditLog(tenantId, "user.created", AuditOutcome.Success,
            actor: new AuditActorInfo(actorId, "user")));
        await repo.AddAsync(new AuditLog(tenantId, "user.deleted", AuditOutcome.Success,
            actor: new AuditActorInfo(Guid.NewGuid(), "system")));

        var byActorType = await repo.GetPagedAsync(new AuditLogFilter(tenantId, AllTenants: false, ActorType: "user"));
        Assert.All(byActorType, a => Assert.Equal("user", a.ActorType));

        var byActorId = await repo.GetPagedAsync(new AuditLogFilter(tenantId, AllTenants: false, ActorId: actorId));
        Assert.All(byActorId, a => Assert.Equal(actorId, a.ActorId));
        Assert.Contains(byActorId, a => a.Action == "user.created");

        var byAction = await repo.GetPagedAsync(new AuditLogFilter(tenantId, AllTenants: false, Action: "user.deleted"));
        Assert.All(byAction, a => Assert.Equal("user.deleted", a.Action));
    }

    [Fact]
    public async Task GetPagedAsyncFiltersByTargetIdAndDateRange()
    {
        var tenantId = await _fixture.GetSeededTenantIdAsync();
        var targetId = Guid.NewGuid().ToString("N");
        await using var context = _fixture.CreateContext();
        var repo = new AuditLogRepository(context);

        await repo.AddAsync(new AuditLog(tenantId, "role.assigned", AuditOutcome.Success,
            target: new AuditTargetInfo(targetId, "role")));

        var byTarget = await repo.GetPagedAsync(new AuditLogFilter(tenantId, AllTenants: false, TargetId: targetId));
        Assert.Contains(byTarget, a => a.TargetId == targetId);

        var future = DateTime.UtcNow.AddDays(1);
        var past = DateTime.UtcNow.AddDays(-1);

        var withinRange = await repo.GetPagedAsync(new AuditLogFilter(tenantId, AllTenants: false, FromDate: past, ToDate: future));
        Assert.Contains(withinRange, a => a.TargetId == targetId);

        var outsideRange = await repo.GetPagedAsync(new AuditLogFilter(tenantId, AllTenants: false, FromDate: future));
        Assert.DoesNotContain(outsideRange, a => a.TargetId == targetId);
    }

    [Fact]
    public async Task GetPagedAsyncAllTenantsIgnoresTheTenantFilter()
    {
        var tenantId = await _fixture.GetSeededTenantIdAsync();
        await using var context = _fixture.CreateContext();
        var otherTenant = new Tenant($"other-{Guid.NewGuid():N}", $"other-{Guid.NewGuid():N}");
        context.Tenants.Add(otherTenant);
        await context.SaveChangesAsync();

        var repo = new AuditLogRepository(context);
        var marker = $"cross-tenant-{Guid.NewGuid():N}";

        await repo.AddAsync(new AuditLog(otherTenant.Id, marker, AuditOutcome.Success));

        var scoped = await repo.GetPagedAsync(new AuditLogFilter(tenantId, AllTenants: false, Action: marker));
        Assert.Empty(scoped);

        var allTenants = await repo.GetPagedAsync(new AuditLogFilter(tenantId, AllTenants: true, Action: marker));
        Assert.Contains(allTenants, a => a.Action == marker);
    }

    [Fact]
    public async Task GetPagedAsyncAndCountAsyncThrowForANullFilter()
    {
        await using var context = _fixture.CreateContext();
        var repo = new AuditLogRepository(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() => repo.GetPagedAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => repo.CountAsync(null!));
    }
}
