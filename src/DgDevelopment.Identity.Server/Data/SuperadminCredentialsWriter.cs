namespace DgDevelopment.Identity.Server.Data;

// Keeps superadmin-credentials.txt (the local dev bootstrap snapshot) in sync whenever the
// system account's own password changes - both on first seed (DbSeeder) and on a later admin
// reset (UsersController). Only ever called for the IsSystemAccount user, never for regular
// tenant users, so this never becomes a plaintext-password log for arbitrary accounts.
public static class SuperadminCredentialsWriter
{
    public static void Write(string contentRoot, string username, string email, string password)
    {
        var path = Path.Combine(contentRoot, "superadmin-credentials.txt");
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

        File.WriteAllText(path, content);
        Console.WriteLine($"SuperAdmin credentials saved to: {path}");
    }
}
