namespace DgDevelopment.Identity.Server.UnitTests.Testing;

using DgDevelopment.Identity.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Xunit;

public class DatabaseFixture<TTestClass> : IAsyncLifetime
    where TTestClass : class
{
    // LocalDB (the previous approach) only exists on Windows, so tests never ran on the Linux
    // self-hosted CI runner. One SQL Server container is started lazily and shared by every test
    // class in the process (starting a fresh container per class - 40+ of them - would be far too
    // slow); each class still gets its own database inside it, same isolation LocalDB gave via
    // separate databases on one shared engine.
    private static readonly Lazy<Task<MsSqlContainer>> SharedContainer = new(StartContainerAsync);

    private static async Task<MsSqlContainer> StartContainerAsync()
    {
        var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest").Build();
        await container.StartAsync();
        return container;
    }

    protected string ConnectionString { get; private set; } = string.Empty;

    public virtual async Task InitializeAsync()
    {
        var container = await SharedContainer.Value;
        ConnectionString = new SqlConnectionStringBuilder(container.GetConnectionString())
        {
            InitialCatalog = $"DgDevelopment.Identity.Tests.{typeof(TTestClass).Name}",
        }.ConnectionString;

        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
        await TestDbSeeder.SeedAsync(context);
    }

    public virtual async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    public IdentityDbContext CreateContext()
        => new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer(ConnectionString)
            .Options);

    public async Task<Guid> GetSeededClientIdAsync(CancellationToken ct = default)
    {
        await using var context = CreateContext();
        return await context.Clients
            .Where(c => c.ClientId == Guid.Parse(TestConstants.AdminClientId))
            .Select(c => c.Id)
            .SingleAsync(ct);
    }

    public async Task<Guid> GetSeededUserIdAsync(CancellationToken ct = default)
    {
        await using var context = CreateContext();
        return await context.Users
            .Where(u => u.Username == TestConstants.SuperAdminUserName)
            .Select(u => u.Id)
            .SingleAsync(ct);
    }

    public async Task<Guid> GetSeededTenantIdAsync(CancellationToken ct = default)
    {
        await using var context = CreateContext();
        return await context.Tenants
            .Where(t => t.Slug == TestConstants.DefaultTenantSlug)
            .Select(t => t.Id)
            .SingleAsync(ct);
    }

    public async Task<Guid> GetSeededPlatformIdAsync(CancellationToken ct = default)
    {
        await using var context = CreateContext();
        return await context.Platforms
            .Where(p => p.Name == "IdentityAdmin")
            .Select(p => p.Id)
            .SingleAsync(ct);
    }
}
