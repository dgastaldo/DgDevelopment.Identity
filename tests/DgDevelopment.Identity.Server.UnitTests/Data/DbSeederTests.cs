namespace DgDevelopment.Identity.Server.UnitTests.Data;

using System.Text.Json;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Server.Data;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

public sealed class DbSeederTests : IClassFixture<DatabaseFixture<DbSeederTests>>
{
    private readonly DatabaseFixture<DbSeederTests> _fixture;

    public DbSeederTests(DatabaseFixture<DbSeederTests> fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void WriteAdminClientConfigForPlatformWritesExpectedJsonWhenPathConfigured()
    {
        var path = Path.Combine(Path.GetTempPath(), $"admin-client-config-{Guid.NewGuid():N}.json");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AdminClientConfigFile"] = path })
            .Build();
        var clientId = Guid.NewGuid();
        const string clientSecret = "test-secret";

        try
        {
            DbSeeder.WriteAdminClientConfigForPlatform(configuration, clientId, clientSecret);

            Assert.True(File.Exists(path));
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            var identity = json.RootElement.GetProperty("Identity");
            Assert.Equal(clientId.ToString(), identity.GetProperty("AdminClientId").GetString());
            Assert.Equal(clientSecret, identity.GetProperty("AdminClientSecret").GetString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteAdminClientConfigForPlatformIsNoOpWhenPathNotConfigured()
    {
        var configuration = new ConfigurationBuilder().Build();

        // No AdminClientConfigFile set - nothing to assert on disk, just that it doesn't throw
        // (e.g. trying to create a directory for a null/empty path).
        var exception = Record.Exception(() =>
            DbSeeder.WriteAdminClientConfigForPlatform(configuration, Guid.NewGuid(), "unused-secret"));

        Assert.Null(exception);
    }

    [Fact]
    public void WriteClientCredentialsWritesExpectedContent()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), $"dbseeder-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(contentRoot);
        var clientId = Guid.NewGuid();
        const string clientSecret = "test-client-secret";

        try
        {
            DbSeeder.WriteClientCredentials(contentRoot, clientId, clientSecret);

            var path = Path.Combine(contentRoot, "admin-client-credentials.txt");
            Assert.True(File.Exists(path));
            var content = File.ReadAllText(path);
            Assert.Contains(clientId.ToString(), content, StringComparison.Ordinal);
            Assert.Contains(clientSecret, content, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public void WriteClientCredentialsDoesNotThrowWhenContentRootIsUnwritable()
    {
        // A file, not a directory - Path.Combine(contentRoot, "...") then fails to write with
        // DirectoryNotFoundException (an IOException), the same shape of failure a read-only
        // container filesystem produces with UnauthorizedAccessException. Covers the "local dev
        // convenience snapshot must not crash seeding" catch path.
        var unwritableContentRoot = Path.Combine(Path.GetTempPath(), $"dbseeder-test-file-{Guid.NewGuid():N}");
        File.WriteAllText(unwritableContentRoot, "");

        try
        {
            var exception = Record.Exception(() =>
                DbSeeder.WriteClientCredentials(unwritableContentRoot, Guid.NewGuid(), "unused-secret"));

            Assert.Null(exception);
        }
        finally
        {
            File.Delete(unwritableContentRoot);
        }
    }

    [Fact]
    public async Task EnsureAdminClientRedirectUriAsyncAddsConfiguredUri()
    {
        var clientId = await _fixture.GetSeededClientIdAsync();
        await using var context = _fixture.CreateContext();

        await DbSeeder.EnsureAdminClientRedirectUriAsync(context, "http://192.168.1.5:8082");

        await using var verifyContext = _fixture.CreateContext();
        var client = await verifyContext.Clients.SingleAsync(c => c.Id == clientId);
        Assert.Contains(client.RedirectUris, r => r.RedirectUri == new Uri("http://192.168.1.5:8082/callback"));
        Assert.Contains(client.PostLogoutRedirectUris, r => r.RedirectUri == new Uri("http://192.168.1.5:8082/"));
        // The hardcoded local Aspire dev URIs seeded alongside the client stay untouched.
        Assert.Contains(client.RedirectUris, r => r.RedirectUri == new Uri("https://localhost:7018/callback"));
    }

    [Fact]
    public async Task EnsureAdminClientRedirectUriAsyncIsIdempotent()
    {
        var clientId = await _fixture.GetSeededClientIdAsync();
        await using var context = _fixture.CreateContext();
        await DbSeeder.EnsureAdminClientRedirectUriAsync(context, "http://192.168.1.5:8082");

        await using var secondContext = _fixture.CreateContext();
        await DbSeeder.EnsureAdminClientRedirectUriAsync(secondContext, "http://192.168.1.5:8082");

        await using var verifyContext = _fixture.CreateContext();
        var client = await verifyContext.Clients.SingleAsync(c => c.Id == clientId);
        Assert.Single(client.RedirectUris, r => r.RedirectUri == new Uri("http://192.168.1.5:8082/callback"));
    }

    [Fact]
    public async Task EnsureAdminClientRedirectUriAsyncIsNoOpWhenBaseUrlNotConfigured()
    {
        await using var context = _fixture.CreateContext();

        var exception = await Record.ExceptionAsync(() =>
            DbSeeder.EnsureAdminClientRedirectUriAsync(context, adminPlatformBaseUrl: null));

        Assert.Null(exception);
    }

    [Fact]
    public async Task EnsureAdminClientRedirectUriAsyncIsNoOpWhenNoAdminClientExists()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(connection)
            .Options);
        await context.Database.EnsureCreatedAsync();

        var exception = await Record.ExceptionAsync(() =>
            DbSeeder.EnsureAdminClientRedirectUriAsync(context, "http://192.168.1.5:8082"));

        Assert.Null(exception);
        await connection.DisposeAsync();
    }
}
