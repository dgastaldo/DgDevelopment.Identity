using DgDevelopment.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DgDevelopment.Identity.Server.Commands;

// Invoked as `dotnet DgDevelopment.Identity.Server.dll reset-mfa <username>` (e.g. via
// `docker compose run --rm identity-server dotnet DgDevelopment.Identity.Server.dll reset-mfa
// identity.superadmin`) instead of the full web app - see Program.cs's early dispatch.
//
// This is the break-glass path for a system account's MFA (the bootstrap SuperAdmin, chiefly) -
// there's no other admin above it to grant a reset through UsersController's
// POST /api/v1/users/{id}/mfa/reset endpoint (which explicitly refuses system accounts, see
// UserService.ResetMfaAsync/EnsureNotSystemAccount), so recovery has to come from a tier only
// someone with shell access to the server already has: this CLI, never the web. Refuses to run
// on a non-system account too, in the other direction - that one has the web endpoint and an
// actual admin to ask, no need for break-glass.
public static class ResetMfaCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
        {
            await Console.Error.WriteLineAsync("Usage: reset-mfa <username>").ConfigureAwait(false);
            return 1;
        }

        var hostBuilder = Host.CreateApplicationBuilder(args);
        var connectionString = hostBuilder.Configuration.GetConnectionString("IdentityDb")
            ?? throw new InvalidOperationException("Connection string 'IdentityDb' not found.");
        hostBuilder.Services.AddInfrastructure(connectionString);

        using var host = hostBuilder.Build();
        var db = host.Services.GetRequiredService<IdentityDbContext>();

        return await ResetAsync(db, args[1], Console.Out, Console.Error).ConfigureAwait(false);
    }

    // Split out from RunAsync so this can be tested against a SQLite in-memory IdentityDbContext,
    // same pattern as RotateAdminClientSecretCommand.RotateAsync.
    public static async Task<int> ResetAsync(IdentityDbContext db, string username, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username).ConfigureAwait(false);
        if (user is null)
        {
            await stderr.WriteLineAsync($"No user found with username '{username}'.").ConfigureAwait(false);
            return 1;
        }

        if (!user.IsSystemAccount)
        {
            await stderr.WriteLineAsync(
                $"'{username}' is not a system account - use the admin UI/API instead " +
                "(POST /api/v1/users/{id}/mfa/reset), which needs no server access.").ConfigureAwait(false);
            return 1;
        }

        var secret = await db.TotpSecrets.Include(s => s.BackupCodes).FirstOrDefaultAsync(s => s.UserId == user.Id).ConfigureAwait(false);
        if (secret is not null)
        {
            secret.Disable();
            secret.RegenerateBackupCodes([]);
        }

        var devices = await db.PushDevices.Where(d => d.UserId == user.Id).ToListAsync().ConfigureAwait(false);
        db.PushDevices.RemoveRange(devices);

        await db.SaveChangesAsync().ConfigureAwait(false);

        await stdout.WriteLineAsync("==============================================").ConfigureAwait(false);
        await stdout.WriteLineAsync($"  MFA reset for '{username}'").ConfigureAwait(false);
        await stdout.WriteLineAsync($"  TOTP disabled:        {secret is not null}").ConfigureAwait(false);
        await stdout.WriteLineAsync($"  Push devices removed: {devices.Count}").ConfigureAwait(false);
        await stdout.WriteLineAsync("  The account must enroll MFA again on next login.").ConfigureAwait(false);
        await stdout.WriteLineAsync("==============================================").ConfigureAwait(false);

        return 0;
    }
}
