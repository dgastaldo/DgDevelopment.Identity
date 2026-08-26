using DgDevelopment.Identity.Application.Tenants;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.OAuth.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Security.Cryptography;

namespace DgDevelopment.Identity.Server.Data;

public sealed class DbSeeder(IServiceProvider serviceProvider)
{

    public async Task SeedAsync()
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var contentRoot = scope.ServiceProvider.GetRequiredService<IHostEnvironment>().ContentRootPath;

        await db.Database.MigrateAsync().ConfigureAwait(false);

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == "identity-tenant").ConfigureAwait(false);
        if (tenant is null)
        {
            var provisioning = scope.ServiceProvider.GetRequiredService<ITenantProvisioningService>();
            var result = await provisioning.ProvisionAsync("Identity Tenant", "identity-tenant", isPlatformTenant: true).ConfigureAwait(false);
            tenant = result.Tenant;
        }

        var clientCredentials = await SeedClientsAsync(db, tenant.Id).ConfigureAwait(false);
        var hasher = scope.ServiceProvider.GetRequiredService<Domain.Services.IPasswordHasher>();
        var superadminPassword = await SeedUsersAsync(db, hasher, tenant.Id).ConfigureAwait(false);

        if (superadminPassword != null)
            SuperadminCredentialsWriter.Write(contentRoot, "identity.superadmin", "identity.superadmin@dgdevelopment.it", superadminPassword);

        if (clientCredentials is { } credentials)
        {
            WriteClientCredentials(contentRoot, credentials.ClientId, credentials.ClientSecret);
            await SyncClientSecretToAppHostAsync(contentRoot, credentials.ClientId, credentials.ClientSecret).ConfigureAwait(false);
        }
    }

    private static async Task<(Guid ClientId, string ClientSecret)?> SeedClientsAsync(IdentityDbContext db, Guid tenantId)
    {
        if (await db.Clients.AnyAsync().ConfigureAwait(false)) return null;

        var platform = await db.Platforms.FirstAsync(p => p.TenantId == tenantId && p.Name == "IdentityAdmin").ConfigureAwait(false);
        var clientSecret = Secret.Generate(32);
        var clientSecretHash = Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(clientSecret)));

        var client = new Client(tenantId, Guid.NewGuid(), clientSecretHash, "identity-platform", ClientType.Confidential, platform.Id);
        client.AddGrantType("authorization_code");
        client.AddGrantType("client_credentials");
        client.AddGrantType("refresh_token");
        client.AddGrantType("device_code");
        client.AddScope("openid");
        client.AddScope("profile");
        client.AddScope("email");
        client.AddRedirectUri(new Uri("https://localhost:7018/callback"));
        client.AddRedirectUri(new Uri("http://localhost:5133/callback"));
        client.AddPostLogoutRedirectUri(new Uri("https://localhost:7018/"));

        db.Clients.Add(client);
        await db.SaveChangesAsync().ConfigureAwait(false);

        Console.WriteLine($"--- IdentityPlatform Client ID: {client.ClientId} ---");
        Console.WriteLine($"--- IdentityPlatform Client Secret: {clientSecret} ---");
        return (client.ClientId, clientSecret);
    }

    private static async Task<string?> SeedUsersAsync(IdentityDbContext db, Domain.Services.IPasswordHasher hasher, Guid tenantId)
    {
        if (await db.Users.AnyAsync().ConfigureAwait(false)) return null;

        var password = Secret.Generate(16);
        var passwordHash = hasher.HashPassword(password);

        var email = EmailAddress.FromString("identity.superadmin@dgdevelopment.it");
        var user = new User("identity.superadmin", passwordHash, email, isSystemAccount: true);
        user.VerifyEmail(email);

        var superAdminsGroup = await db.Groups.FirstAsync(g => g.TenantId == tenantId && g.Name == "SuperAdmins").ConfigureAwait(false);
        user.AddToGroup(superAdminsGroup);

        db.Users.Add(user);
        db.TenantMemberships.Add(new TenantMembership(tenantId, user.Id, isOwner: true));
        await db.SaveChangesAsync().ConfigureAwait(false);

        Console.WriteLine("==============================================");
        Console.WriteLine("  SUPERADMIN CREDENTIALS (SAVE THESE!)");
        Console.WriteLine("  Username: identity.superadmin");
        Console.WriteLine($"  Password: {password}");
        Console.WriteLine("  Email:    identity.superadmin@dgdevelopment.it");
        Console.WriteLine("==============================================");

        return password;
    }

    private static void WriteClientCredentials(string contentRoot, Guid clientId, string clientSecret)
    {
        var path = Path.Combine(contentRoot, "admin-client-credentials.txt");
        var content = $"""
        ==============================================
          DgDevelopment Identity - Admin Client Credentials
        ==============================================
          Client ID: {clientId}
          Client Secret: {clientSecret}
        ==============================================
          Store this file in a secure location.
          Do not commit to version control.
        ==============================================
        """;

        File.WriteAllText(path, content);
        Console.WriteLine($"Admin client credentials saved to: {path}");
    }

    private static async Task SyncClientSecretToAppHostAsync(string contentRoot, Guid clientId, string clientSecret, CancellationToken ct = default)
    {
        var appHostDir = Path.GetFullPath(Path.Combine(contentRoot, "..", "DgDevelopment.Identity.AppHost"));

        if (!Directory.Exists(appHostDir))
        {
            Console.WriteLine($"AppHost secret sync skipped: directory not found at {appHostDir}");
            return;
        }

        try
        {
            foreach (var (key, value) in new[]
                     {
                         ("Identity:AdminClientId", clientId.ToString()),
                         ("Identity:AdminClientSecret", clientSecret)
                     })
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"user-secrets set \"{key}\" \"{value}\" --project \"{appHostDir}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };

                using var process = Process.Start(startInfo);
                if (process is null)
                    return;

                var output = await process.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
                var error = await process.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
                await process.WaitForExitAsync(ct).ConfigureAwait(false);

                Console.WriteLine(output.Trim());
                if (!string.IsNullOrWhiteSpace(error))
                    Console.WriteLine(error.Trim());
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException or OperationCanceledException)
        {
            Console.WriteLine($"AppHost secret sync failed: {ex.Message}");
        }
    }
}
