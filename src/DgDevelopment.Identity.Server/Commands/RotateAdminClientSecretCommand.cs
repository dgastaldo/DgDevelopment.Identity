using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DgDevelopment.Identity.Server.Commands;

// Invoked as `dotnet DgDevelopment.Identity.Server.dll rotate-admin-client-secret` (e.g. via
// `docker compose run --rm identity-server dotnet DgDevelopment.Identity.Server.dll
// rotate-admin-client-secret`) instead of the full web app - see Program.cs's early dispatch.
// Only the secret rotates; ClientId is the client's business key (referenced by tokens/redirect
// config) and Client.cs deliberately has no way to change it after creation.
public static class RotateAdminClientSecretCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var hostBuilder = Host.CreateApplicationBuilder(args);
        var connectionString = hostBuilder.Configuration.GetConnectionString("IdentityDb")
            ?? throw new InvalidOperationException("Connection string 'IdentityDb' not found.");
        hostBuilder.Services.AddInfrastructure(connectionString);

        using var host = hostBuilder.Build();
        var db = host.Services.GetRequiredService<IdentityDbContext>();
        var adminClientConfigFile = host.Services.GetRequiredService<IConfiguration>()["AdminClientConfigFile"];

        return await RotateAsync(db, adminClientConfigFile, Console.Out, Console.Error).ConfigureAwait(false);
    }

    // Split out from RunAsync so this can be tested against a SQLite in-memory IdentityDbContext
    // (see DatabaseFixture<T> in Server.UnitTests) instead of needing a real SQL Server - RunAsync
    // itself is just host/config wiring, not worth testing on its own.
    public static async Task<int> RotateAsync(
        IdentityDbContext db, string? adminClientConfigFile, TextWriter stdout, TextWriter stderr)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Name == "identity-platform").ConfigureAwait(false);
        if (client is null)
        {
            await stderr.WriteLineAsync("No 'identity-platform' admin client found - has the database been seeded yet?").ConfigureAwait(false);
            return 1;
        }

        var newSecret = Secret.Generate(32);
        var newSecretHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(newSecret)));
        client.SetSecret(newSecretHash);
        await db.SaveChangesAsync().ConfigureAwait(false);

        await stdout.WriteLineAsync("==============================================").ConfigureAwait(false);
        await stdout.WriteLineAsync("  Admin client secret rotated").ConfigureAwait(false);
        await stdout.WriteLineAsync($"  Client ID:     {client.ClientId}").ConfigureAwait(false);
        await stdout.WriteLineAsync($"  Client Secret: {newSecret}").ConfigureAwait(false);
        await stdout.WriteLineAsync("==============================================").ConfigureAwait(false);

        // Same shared-volume file DbSeeder writes on first seed - see its
        // WriteAdminClientConfigForPlatform for the full rationale. Keeps rotation fully
        // automatic too: identity-platform just needs a restart to pick up the new file.
        if (!string.IsNullOrWhiteSpace(adminClientConfigFile))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(adminClientConfigFile) ?? ".");
            var json = JsonSerializer.Serialize(new
            {
                Identity = new { AdminClientId = client.ClientId.ToString(), AdminClientSecret = newSecret }
            });
            await File.WriteAllTextAsync(adminClientConfigFile, json).ConfigureAwait(false);
            await stdout.WriteLineAsync($"Admin client config for identity-platform written to: {adminClientConfigFile}").ConfigureAwait(false);
            await stdout.WriteLineAsync("Restart identity-platform to pick it up: docker compose restart identity-platform").ConfigureAwait(false);
        }
        else
        {
            await stdout.WriteLineAsync("Set these as identity-platform's Identity__AdminClientId/Identity__AdminClientSecret and restart it.").ConfigureAwait(false);
        }

        return 0;
    }
}
