using Microsoft.Extensions.Configuration;

namespace DgDevelopment.Identity.Server.Data;

// Keeps superadmin-credentials.txt in sync whenever the system account's own password changes -
// both on first seed (DbSeeder) and on a later admin reset (UsersController). Only ever called
// for the IsSystemAccount user, never for regular tenant users, so this never becomes a
// plaintext-password log for arbitrary accounts.
public static class SuperadminCredentialsWriter
{
    // SuperadminCredentialsFile overrides where this goes - e.g. a volume shared with another
    // container (see deploy/personal/docker-compose.yml), alongside AdminClientConfigFile. Falls
    // back to contentRoot (the local dev bootstrap snapshot's original location) when unset.
    public static void Write(IConfiguration configuration, string contentRoot, string username, string email, string password)
    {
        var path = configuration["SuperadminCredentialsFile"] ?? Path.Combine(contentRoot, "superadmin-credentials.txt");
        var content = $"""
        ==============================================
          DgDevelopment Identity - SuperAdmin Credentials
        ==============================================
          Username: {username}
          Password: {password}
          Email:    {email}
        ==============================================
          Store this file in a secure location.
          Do not commit to version control.
        ==============================================
        """;

        try
        {
            File.WriteAllText(path, content);
            Console.WriteLine($"SuperAdmin credentials saved to: {path}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Optional convenience snapshot only (the credentials are already on the console above) -
            // a containerized deploy's read-only content root must not crash startup over this.
            Console.WriteLine($"Could not write {path}, skipping (credentials are in the console output above): {ex.Message}");
        }
    }
}
