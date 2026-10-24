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
            Assert.Contains(clientId.ToString(), content);
            Assert.Contains(clientSecret, content);
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
}
