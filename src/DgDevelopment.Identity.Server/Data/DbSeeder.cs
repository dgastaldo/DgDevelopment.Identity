using DgDevelopment.Identity.Application.Tenants;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.OAuth.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace DgDevelopment.Identity.Server.Data;

public sealed class DbSeeder(IServiceProvider serviceProvider, IConfiguration configuration)
{

    public async Task SeedAsync()
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var contentRoot = scope.ServiceProvider.GetRequiredService<IHostEnvironment>().ContentRootPath;

        await db.Database.MigrateAsync().ConfigureAwait(false);

        var provisioning = scope.ServiceProvider.GetRequiredService<ITenantProvisioningService>();
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == "identity-tenant").ConfigureAwait(false);
        if (tenant is null)
        {
            var result = await provisioning.ProvisionAsync("Identity Tenant", "identity-tenant", isPlatformTenant: true).ConfigureAwait(false);
            tenant = result.Tenant;
        }

        // Self-healing: backfills any permission/role added to StandardPermissionCatalog after a
        // tenant was already provisioned, on every startup - see ReconcileAsync.
        var tenantIds = await db.Tenants.Select(t => t.Id).ToListAsync().ConfigureAwait(false);
        foreach (var tenantId in tenantIds)
            await provisioning.ReconcileAsync(tenantId).ConfigureAwait(false);

        var clientCredentials = await SeedClientsAsync(db, tenant.Id).ConfigureAwait(false);
        await SeedMauiClientAsync(db, tenant.Id).ConfigureAwait(false);
        await EnsureAdminClientRedirectUriAsync(db, configuration["AdminPlatformBaseUrl"]).ConfigureAwait(false);
        var hasher = scope.ServiceProvider.GetRequiredService<Domain.Services.IPasswordHasher>();
        var superadminPassword = await SeedUsersAsync(db, hasher, tenant.Id).ConfigureAwait(false);

        if (superadminPassword != null)
            SuperadminCredentialsWriter.Write(configuration, contentRoot, "identity.superadmin", "identity.superadmin@dgdevelopment.it", superadminPassword);

        if (clientCredentials is { } credentials)
        {
            WriteClientCredentials(contentRoot, credentials.ClientId, credentials.ClientSecret);
            await SyncClientSecretToAppHostAsync(contentRoot, credentials.ClientId, credentials.ClientSecret).ConfigureAwait(false);
            WriteAdminClientConfigForPlatform(configuration, credentials.ClientId, credentials.ClientSecret);
        }
    }

    // Writes the admin client id/secret to a file on a volume shared with identity-platform (see
    // deploy/personal/docker-compose.yml's `admin-client-config` volume and
    // IdentityPlatform/Program.cs, which layers this file on top of its own configuration) - so
    // identity-platform picks up fresh credentials automatically on its next start, no manual copy
    // step. A no-op when AdminClientConfigFile isn't set (e.g. local dev), same as
    // SyncClientSecretToAppHostAsync above for that case.
    internal static void WriteAdminClientConfigForPlatform(IConfiguration configuration, Guid clientId, string clientSecret)
    {
        var path = configuration["AdminClientConfigFile"];
        if (string.IsNullOrWhiteSpace(path)) return;

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        var json = JsonSerializer.Serialize(new
        {
            Identity = new { AdminClientId = clientId.ToString(), AdminClientSecret = clientSecret }
        });
        File.WriteAllText(path, json);
        Console.WriteLine($"Admin client config for identity-platform written to: {path}");
    }

    // Self-healing like the permission/role reconciliation above: the "identity-platform" client is
    // seeded once with only the local Aspire dev redirect URIs (https://localhost:7018/callback,
    // http://localhost:5133/callback) - a container deployment's identity-platform is reachable at
    // a different, environment-specific URL (see deploy/personal/docker-compose.yml's
    // AdminPlatformBaseUrl), which the hardcoded dev URIs never cover. Runs on every startup, not
    // just first seed, so an existing client - already seeded before this URL was configured, or
    // before this method existed - still gets the redirect URI it needs. AddRedirectUri/
    // AddPostLogoutRedirectUri are no-ops if already present, so this is safe to call every time.
    // A no-op when AdminPlatformBaseUrl isn't set (e.g. local dev, Aspire), same pattern as
    // AdminClientConfigFile elsewhere in this class.
    internal static async Task EnsureAdminClientRedirectUriAsync(IdentityDbContext db, string? adminPlatformBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(adminPlatformBaseUrl)) return;

        var client = await db.Clients.FirstOrDefaultAsync(c => c.Name == "identity-platform").ConfigureAwait(false);
        if (client is null) return;

        // UriBuilder.Uri normalizes a path-less base to "/" on its own - avoids a hand-built
        // trailing separator, which also reads as a hardcoded path to static analysis
        // (csharpsquid:S1075).
        var baseUri = new UriBuilder(adminPlatformBaseUrl).Uri;
        client.AddRedirectUri(new Uri(baseUri, "callback"));
        client.AddPostLogoutRedirectUri(baseUri);
        await db.SaveChangesAsync().ConfigureAwait(false);
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

    // The GUID here must match clients/DgDevelopment.Identity.Client.Maui/AppConfig.cs's ClientId
    // constant exactly - the MAUI app has no secret to authenticate with (it's a Public/PKCE-only
    // client), so its identity is this well-known Id rather than something generated per install.
    // Runs unconditionally (unlike SeedClientsAsync, which only ever seeds once) so the MAUI
    // client keeps working even on a database that already had other clients before this existed.
    private static async Task SeedMauiClientAsync(IdentityDbContext db, Guid tenantId)
    {
        var mauiClientId = Guid.Parse("8f2b1a4c-6d3e-4a7b-9c1d-2e5f6a7b8c9d");
        if (await db.Clients.AnyAsync(c => c.ClientId == mauiClientId).ConfigureAwait(false))
            return;

        var platform = await db.Platforms.FirstAsync(p => p.TenantId == tenantId && p.Name == "IdentityAdmin").ConfigureAwait(false);

        var client = new Client(tenantId, mauiClientId, clientSecretHash: string.Empty, "MAUI App", ClientType.Public, platform.Id);
        client.AddGrantType("authorization_code");
        client.AddGrantType("refresh_token");
        client.AddScope("openid");
        client.AddScope("profile");
        client.AddScope("email");
        client.AddRedirectUri(new Uri("dgidentityapp://callback"));
        client.AddPostLogoutRedirectUri(new Uri("dgidentityapp://callback"));

        db.Clients.Add(client);
        await db.SaveChangesAsync().ConfigureAwait(false);

        Console.WriteLine($"--- MAUI App Client ID (Public/PKCE, no secret): {client.ClientId} ---");
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

    internal static void WriteClientCredentials(string contentRoot, Guid clientId, string clientSecret)
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

        try
        {
            File.WriteAllText(path, content);
            Console.WriteLine($"Admin client credentials saved to: {path}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Optional convenience snapshot only (the credentials are already on the console above) -
            // a containerized deploy's read-only content root must not crash startup over this.
            Console.WriteLine($"Could not write {path}, skipping (credentials are in the console output above): {ex.Message}");
        }
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
                // Environment.ProcessPath is the dotnet muxer that launched this process (dev-only
                // seeding always runs under `dotnet run`/AppHost) - an absolute path, so this can't
                // pick up some other "dotnet" earlier on PATH.
                var startInfo = new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath ?? "dotnet",
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
