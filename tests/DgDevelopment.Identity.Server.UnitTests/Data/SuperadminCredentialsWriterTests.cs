namespace DgDevelopment.Identity.Server.UnitTests.Data;

using DgDevelopment.Identity.Server.Data;
using Xunit;

public sealed class SuperadminCredentialsWriterTests
{
    [Fact]
    public void WriteWritesExpectedContent()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), $"superadmin-writer-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(contentRoot);
        const string username = "identity.superadmin";
        const string email = "identity.superadmin@dgdevelopment.it";
        const string password = "test-password";

        try
        {
            SuperadminCredentialsWriter.Write(contentRoot, username, email, password);

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
    public void WriteDoesNotThrowWhenContentRootIsUnwritable()
    {
        // Same shape of failure a read-only container filesystem produces (UnauthorizedAccessException) -
        // a file, not a directory, makes Path.Combine(contentRoot, "...") fail to write with
        // DirectoryNotFoundException (an IOException) instead, exercising the same catch path.
        var unwritableContentRoot = Path.Combine(Path.GetTempPath(), $"superadmin-writer-test-file-{Guid.NewGuid():N}");
        File.WriteAllText(unwritableContentRoot, "");

        try
        {
            var exception = Record.Exception(() =>
                SuperadminCredentialsWriter.Write(unwritableContentRoot, "identity.superadmin", "identity.superadmin@dgdevelopment.it", "unused-password"));

            Assert.Null(exception);
        }
        finally
        {
            File.Delete(unwritableContentRoot);
        }
    }
}
