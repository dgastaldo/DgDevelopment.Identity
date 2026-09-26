namespace DgDevelopment.Identity.Server.UnitTests.MultiTenant;

using DgDevelopment.Identity.Application.Audit;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Xunit;

public sealed class AuditLogServiceTests : IClassFixture<DatabaseFixture<AuditLogServiceTests>>
{
    private readonly DatabaseFixture<AuditLogServiceTests> _fixture;

    public AuditLogServiceTests(DatabaseFixture<AuditLogServiceTests> fixture)
    {
        _fixture = fixture;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static AuditLogService CreateService(IdentityDbContext context)
        => new(new AuditLogRepository(context));

    private static async Task<Tenant> CreateTenantAsync(IdentityDbContext context, string name)
    {
        var tenant = new Tenant(name, Unique(name.ToUpperInvariant()));
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
        return tenant;
    }

    private static async Task AddLogAsync(IdentityDbContext context, Guid tenantId, string action, string? targetId = null, AuditOutcome outcome = AuditOutcome.Success)
    {
        context.AuditLogs.Add(new AuditLog(tenantId, action, outcome, targetId: targetId));
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetPagedAsyncOnlyReturnsLogsOfTheActiveTenantUnlessAllTenants()
    {
        await using var context = _fixture.CreateContext();
        var tenantA = await CreateTenantAsync(context, "AuditPagedA");
        var tenantB = await CreateTenantAsync(context, "AuditPagedB");
        await AddLogAsync(context, tenantA.Id, "tenant-a.action");
        await AddLogAsync(context, tenantB.Id, "tenant-b.action");
        var service = CreateService(context);

        var scopedResult = await service.GetPagedAsync(tenantA.Id, allTenants: false, null, null, null, null, null, null, 1, 100);
        var globalResult = await service.GetPagedAsync(tenantA.Id, allTenants: true, null, null, null, null, null, null, 1, 1000);

        Assert.Contains(scopedResult.Items, a => a.Action == "tenant-a.action");
        Assert.DoesNotContain(scopedResult.Items, a => a.Action == "tenant-b.action");
        Assert.Contains(globalResult.Items, a => a.Action == "tenant-a.action");
        Assert.Contains(globalResult.Items, a => a.Action == "tenant-b.action");
    }

    [Fact]
    public async Task GetPagedAsyncFiltersByAction()
    {
        await using var context = _fixture.CreateContext();
        var tenant = await CreateTenantAsync(context, "AuditFilterAction");
        await AddLogAsync(context, tenant.Id, "user.login");
        await AddLogAsync(context, tenant.Id, "user.logout");
        var service = CreateService(context);

        var result = await service.GetPagedAsync(tenant.Id, allTenants: false, null, null, "user.login", null, null, null, 1, 100);

        Assert.All(result.Items, a => Assert.Equal("user.login", a.Action));
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task GetPagedAsyncFiltersByTargetId()
    {
        await using var context = _fixture.CreateContext();
        var tenant = await CreateTenantAsync(context, "AuditFilterTarget");
        var targetId = Guid.NewGuid().ToString();
        await AddLogAsync(context, tenant.Id, "role.create", targetId: targetId);
        await AddLogAsync(context, tenant.Id, "role.create", targetId: Guid.NewGuid().ToString());
        var service = CreateService(context);

        var result = await service.GetPagedAsync(tenant.Id, allTenants: false, null, null, "role.create", targetId, null, null, 1, 100);

        var entry = Assert.Single(result.Items);
        Assert.Equal(targetId, entry.TargetId);
    }

    [Fact]
    public async Task GetPagedAsyncReportsCorrectTotalCountAcrossPages()
    {
        await using var context = _fixture.CreateContext();
        var tenant = await CreateTenantAsync(context, "AuditPaging");
        for (var i = 0; i < 5; i++)
            await AddLogAsync(context, tenant.Id, $"action-{i}");
        var service = CreateService(context);

        var firstPage = await service.GetPagedAsync(tenant.Id, allTenants: false, null, null, null, null, null, null, 1, 2);

        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal(5, firstPage.TotalCount);
        Assert.Equal(3, firstPage.TotalPages);
    }
}
