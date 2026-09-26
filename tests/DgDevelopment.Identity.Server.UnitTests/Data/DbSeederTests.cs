namespace DgDevelopment.Identity.Server.UnitTests.Data;

using System.Text.Json;
using DgDevelopment.Identity.Server.Data;
using Microsoft.Extensions.Configuration;
using Xunit;

public sealed class DbSeederTests
{
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

        DbSeeder.WriteAdminClientConfigForPlatform(configuration, Guid.NewGuid(), "unused-secret");

        // No AdminClientConfigFile set - nothing to assert on disk, this just confirms it doesn't
        // throw (e.g. trying to create a directory for a null/empty path).
    }
}
