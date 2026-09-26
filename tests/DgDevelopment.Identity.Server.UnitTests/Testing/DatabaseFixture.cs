namespace DgDevelopment.Identity.Server.UnitTests.Testing;

using DgDevelopment.Identity.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

// Was a shared Testcontainers SQL Server instance (LocalDB, the previous approach, only exists on
// Windows so never ran on the Linux CI runner) - moved to a private SQLite in-memory database per
// fixture instance instead. No Docker container, no shared-instance memory tuning to fight over
// (see git history for the MSSQL_MEMORY_LIMIT_MB saga that motivated this move), and each of the
// 40+ test classes using this fixture now gets a fully isolated database instead of a separate
// database on one shared engine - xunit.runner.json's parallelizeTestCollections can go back to
// true as a result.
//
// A SQLite in-memory database (`Data Source=:memory:`) lives only as long as its one connection
// stays open, and a fresh SqliteConnection against that same connection string is a *different*,
// empty database - not the same one. So this keeps one connection open for the fixture's lifetime
// and hands it to every DbContext via UseSqlite(connection) (not a connection string), which is
// the standard EF Core pattern for this: https://learn.microsoft.com/ef/core/testing/testing-sample
//
// EnsureCreated() (from the current model snapshot) instead of MigrateAsync(): the SQL Server
// migrations under Infrastructure/Migrations contain provider-specific SQL (raw NEWID() etc.) that
// doesn't apply to SQLite, and tests don't need migration history anyway, just the current schema.
public class DatabaseFixture<TTestClass> : IAsyncLifetime
    where TTestClass : class
{
    private SqliteConnection _connection = null!;

    public virtual async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
        await TestDbSeeder.SeedAsync(context);
    }

    public virtual async Task DisposeAsync()
        => await _connection.DisposeAsync();

    public IdentityDbContext CreateContext()
        => new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(_connection)
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
