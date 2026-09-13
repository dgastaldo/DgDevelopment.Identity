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

        var client = await db.Clients.FirstOrDefaultAsync(c => c.Name == "identity-platform").ConfigureAwait(false);
        if (client is null)
        {
            await Console.Error.WriteLineAsync("No 'identity-platform' admin client found - has the database been seeded yet?").ConfigureAwait(false);
            return 1;
        }

        var newSecret = Secret.Generate(32);
        var newSecretHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(newSecret)));
        client.SetSecret(newSecretHash);
        await db.SaveChangesAsync().ConfigureAwait(false);

        Console.WriteLine("==============================================");
        Console.WriteLine("  Admin client secret rotated");
        Console.WriteLine($"  Client ID:     {client.ClientId}");
        Console.WriteLine($"  Client Secret: {newSecret}");
        Console.WriteLine("==============================================");

        // Same shared-volume file DbSeeder writes on first seed - see its
        // WriteAdminClientConfigForPlatform for the full rationale. Keeps rotation fully
        // automatic too: identity-platform just needs a restart to pick up the new file.
        var path = host.Services.GetRequiredService<IConfiguration>()["AdminClientConfigFile"];
        if (!string.IsNullOrWhiteSpace(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            var json = JsonSerializer.Serialize(new
            {
                Identity = new { AdminClientId = client.ClientId.ToString(), AdminClientSecret = newSecret }
            });
            await File.WriteAllTextAsync(path, json).ConfigureAwait(false);
            Console.WriteLine($"Admin client config for identity-platform written to: {path}");
            Console.WriteLine("Restart identity-platform to pick it up: docker compose restart identity-platform");
        }
        else
        {
            Console.WriteLine("Set these as identity-platform's Identity__AdminClientId/Identity__AdminClientSecret and restart it.");
        }

        return 0;
    }
}
