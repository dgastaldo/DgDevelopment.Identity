namespace DgDevelopment.Identity.Server.UnitTests.Data;

using DgDevelopment.Identity.Server.Data;
using Microsoft.Extensions.Configuration;
using Xunit;

public sealed class SuperadminCredentialsWriterTests
{
    [Fact]
    public void WriteWritesExpectedContent()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), $"superadmin-writer-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(contentRoot);
        var configuration = new ConfigurationBuilder().Build();
        const string username = "identity.superadmin";
        const string email = "identity.superadmin@dgdevelopment.it";
        const string password = "test-password";

        try
        {
            SuperadminCredentialsWriter.Write(configuration, contentRoot, username, email, password);

            var path = Path.Combine(contentRoot, "superadmin-credentials.txt");
            Assert.True(File.Exists(path));
            var content = File.ReadAllText(path);
            Assert.Contains(username, content, StringComparison.Ordinal);
            Assert.Contains(email, content, StringComparison.Ordinal);
            Assert.Contains(password, content, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public void WriteUsesSuperadminCredentialsFileWhenConfigured()
    {
        var path = Path.Combine(Path.GetTempPath(), $"superadmin-writer-test-{Guid.NewGuid():N}.txt");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["SuperadminCredentialsFile"] = path })
            .Build();

        try
        {
            SuperadminCredentialsWriter.Write(configuration, contentRoot: Path.GetTempPath(), "identity.superadmin", "identity.superadmin@dgdevelopment.it", "test-password");

            Assert.True(File.Exists(path));
            Assert.Contains("test-password", File.ReadAllText(path), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteDoesNotThrowWhenContentRootIsUnwritable()
    {
        // Same shape of failure a read-only container filesystem produces (UnauthorizedAccessException) -
        // a file, not a directory, makes Path.Combine(contentRoot, "...") fail to write with
        // DirectoryNotFoundException (an IOException) instead, exercising the same catch path.
        var unwritableContentRoot = Path.Combine(Path.GetTempPath(), $"superadmin-writer-test-file-{Guid.NewGuid():N}");
        File.WriteAllText(unwritableContentRoot, "");
        var configuration = new ConfigurationBuilder().Build();

        try
        {
            var exception = Record.Exception(() =>
                SuperadminCredentialsWriter.Write(configuration, unwritableContentRoot, "identity.superadmin", "identity.superadmin@dgdevelopment.it", "unused-password"));

            Assert.Null(exception);
        }
        finally
        {
            File.Delete(unwritableContentRoot);
        }
    }
}
