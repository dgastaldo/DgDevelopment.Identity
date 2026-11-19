namespace DgDevelopment.Identity.Server.UnitTests.Commands;

using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Server.Commands;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

public sealed class ResetMfaCommandTests : IClassFixture<DatabaseFixture<ResetMfaCommandTests>>
{
    private readonly DatabaseFixture<ResetMfaCommandTests> _fixture;

    public ResetMfaCommandTests(DatabaseFixture<ResetMfaCommandTests> fixture)
    {
        _fixture = fixture;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    [Fact]
    public async Task ResetAsyncDisablesTotpAndRemovesPushDevicesForSystemAccount()
    {
        var username = Unique("superadmin");
        Guid userId;
        await using (var setupContext = _fixture.CreateContext())
        {
            var user = new User(username, "hash", EmailAddress.FromString($"{Guid.NewGuid():N}@example.com"), isSystemAccount: true);
            setupContext.Users.Add(user);

            var secret = new TotpSecret(user.Id, "protected-secret");
            secret.Enable();
            secret.AddBackupCode("hash-1");
            setupContext.TotpSecrets.Add(secret);

            setupContext.PushDevices.Add(new PushDevice(user.Id, PushPlatform.Android, "push-token", "My phone"));

            await setupContext.SaveChangesAsync();
            userId = user.Id;
        }

        await using var context = _fixture.CreateContext();
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await ResetMfaCommand.ResetAsync(context, username, stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Empty(stderr.ToString());
        Assert.Contains("MFA reset", stdout.ToString(), StringComparison.Ordinal);

        await using var verifyContext = _fixture.CreateContext();
        var storedSecret = await verifyContext.TotpSecrets.SingleAsync(s => s.UserId == userId);
        Assert.False(storedSecret.IsEnabled);
        var remainingDevices = await verifyContext.PushDevices.Where(d => d.UserId == userId).ToListAsync();
        Assert.Empty(remainingDevices);
    }

    [Fact]
    public async Task ResetAsyncRefusesANonSystemAccount()
    {
        var username = Unique("regular-user");
        await using (var setupContext = _fixture.CreateContext())
        {
            var user = new User(username, "hash", EmailAddress.FromString($"{Guid.NewGuid():N}@example.com"));
            setupContext.Users.Add(user);
            await setupContext.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await ResetMfaCommand.ResetAsync(context, username, stdout, stderr);

        Assert.Equal(1, exitCode);
        Assert.Contains("is not a system account", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResetAsyncReturnsOneWhenUserNotFound()
    {
        await using var context = _fixture.CreateContext();
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await ResetMfaCommand.ResetAsync(context, "no-such-user", stdout, stderr);

        Assert.Equal(1, exitCode);
        Assert.Contains("No user found", stderr.ToString(), StringComparison.Ordinal);
    }
}
