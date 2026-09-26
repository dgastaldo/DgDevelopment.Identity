namespace DgDevelopment.Identity.Server.UnitTests.Commands;

using System.Text.Json;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Server.Commands;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

public sealed class RotateAdminClientSecretCommandTests : IClassFixture<DatabaseFixture<RotateAdminClientSecretCommandTests>>
{
    private readonly DatabaseFixture<RotateAdminClientSecretCommandTests> _fixture;

    public RotateAdminClientSecretCommandTests(DatabaseFixture<RotateAdminClientSecretCommandTests> fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RotateAsyncUpdatesSecretAndReturnsZero()
    {
        var originalClientId = await _fixture.GetSeededClientIdAsync();
        await using var context = _fixture.CreateContext();
        var originalHash = (await context.Clients.SingleAsync(c => c.Id == originalClientId)).ClientSecretHash;
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = await RotateAdminClientSecretCommand.RotateAsync(context, adminClientConfigFile: null, stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Empty(stderr.ToString());
        Assert.Contains("Admin client secret rotated", stdout.ToString());
        await using var verifyContext = _fixture.CreateContext();
        var updated = await verifyContext.Clients.SingleAsync(c => c.Id == originalClientId);
        Assert.NotEqual(originalHash, updated.ClientSecretHash);
    }

    [Fact]
    public async Task RotateAsyncWritesAdminClientConfigFileWhenPathProvided()
    {
        await using var context = _fixture.CreateContext();
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var path = Path.Combine(Path.GetTempPath(), $"admin-client-config-{Guid.NewGuid():N}.json");

        try
        {
            var exitCode = await RotateAdminClientSecretCommand.RotateAsync(context, path, stdout, stderr);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(path));
            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            var identity = json.RootElement.GetProperty("Identity");
            Assert.True(Guid.TryParse(identity.GetProperty("AdminClientId").GetString(), out _));
            Assert.False(string.IsNullOrWhiteSpace(identity.GetProperty("AdminClientSecret").GetString()));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RotateAsyncReturnsOneWhenNoAdminClientExists()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(connection)
            .Options);
        await context.Database.EnsureCreatedAsync();
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = await RotateAdminClientSecretCommand.RotateAsync(context, adminClientConfigFile: null, stdout, stderr);

        Assert.Equal(1, exitCode);
        Assert.Contains("has the database been seeded", stderr.ToString());
    }
}
