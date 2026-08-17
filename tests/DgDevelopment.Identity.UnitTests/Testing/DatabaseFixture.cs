namespace DgDevelopment.Identity.UnitTests.Testing;

using DgDevelopment.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class DatabaseFixture<TTestClass> : IAsyncLifetime
    where TTestClass : class
{
    private const string Server = "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=true";

    public DatabaseFixture()
    {
        ConnectionString = $"{Server};Database=DgDevelopment.Identity.Tests.{typeof(TTestClass).Name}";
    }

    protected string ConnectionString { get; }

    public virtual async Task InitializeAsync()
    {
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
            .Where(c => c.ClientId == TestConstants.AdminClientId)
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
}