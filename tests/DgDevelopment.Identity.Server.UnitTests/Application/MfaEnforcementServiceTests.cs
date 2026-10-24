namespace DgDevelopment.Identity.Server.UnitTests.Application;

using DgDevelopment.Identity.Application.Services;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Xunit;

public sealed class MfaEnforcementServiceTests : IClassFixture<DatabaseFixture<MfaEnforcementServiceTests>>
{
    private readonly DatabaseFixture<MfaEnforcementServiceTests> _fixture;

    public MfaEnforcementServiceTests(DatabaseFixture<MfaEnforcementServiceTests> fixture)
    {
        _fixture = fixture;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static async Task<Tenant> CreateTenantAsync(IdentityDbContext context)
    {
        var tenant = new Tenant(Unique("tenant"), Unique("tenant"));
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
        return tenant;
    }

    private static async Task<Role> CreateGlobalAdminRoleAsync(IdentityDbContext context, Guid tenantId)
    {
        var platform = new Platform(tenantId, Unique("Platform"), "description", PermissionMode.AuthOnly);
        context.Platforms.Add(platform);
        await context.SaveChangesAsync();

        var role = new Role(tenantId, platform.Id, "GlobalAdmin", "desc");
        context.Roles.Add(role);
        await context.SaveChangesAsync();
        return role;
    }

    private static async Task<User> CreateUserAsync(IdentityDbContext context)
    {
        var user = new User(Unique("user"), "hash", EmailAddress.FromString($"{Guid.NewGuid():N}@example.com"));
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static MfaEnforcementService CreateService(IdentityDbContext context, bool hasTotp = false, bool hasPush = false)
        => new(new UserAuthorizationRepository(context), new UserRepository(context), new FakeTotpService(hasTotp), new FakePushMfaService(hasPush));

    [Fact]
    public async Task NonPrivilegedUserNeverNeedsToEnroll()
    {
        await using var context = _fixture.CreateContext();
        var tenant = await CreateTenantAsync(context);
        var user = await CreateUserAsync(context);
        var service = CreateService(context);

        var result = await service.MustEnrollMfaBeforeProceedingAsync(user.Id, tenant.Id);

        Assert.False(result);
    }

    [Fact]
    public async Task PrivilegedUserWithMfaAlreadyEnrolledNeverNeedsToEnrollAgain()
    {
        await using var context = _fixture.CreateContext();
        var tenant = await CreateTenantAsync(context);
        var role = await CreateGlobalAdminRoleAsync(context, tenant.Id);
        var user = await CreateUserAsync(context);
        user.AssignRole(role);
        context.Users.Update(user);
        await context.SaveChangesAsync();

        var service = CreateService(context, hasTotp: true);

        var result = await service.MustEnrollMfaBeforeProceedingAsync(user.Id, tenant.Id);

        Assert.False(result);
    }

    [Fact]
    public async Task PrivilegedUserWithNoMfaGetsAGracePeriodInsteadOfBeingBlockedImmediately()
    {
        Guid userId;
        Guid tenantId;
        await using (var setupContext = _fixture.CreateContext())
        {
            var tenant = await CreateTenantAsync(setupContext);
            var role = await CreateGlobalAdminRoleAsync(setupContext, tenant.Id);
            var user = await CreateUserAsync(setupContext);
            user.AssignRole(role);
            setupContext.Users.Update(user);
            await setupContext.SaveChangesAsync();
            userId = user.Id;
            tenantId = tenant.Id;
        }

        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        var result = await service.MustEnrollMfaBeforeProceedingAsync(userId, tenantId);

        Assert.False(result);

        await using var verifyContext = _fixture.CreateContext();
        var stored = await new UserRepository(verifyContext).GetByIdAsync(userId);
        Assert.NotNull(stored!.MfaGracePeriodStartedAt);
    }

    [Fact]
    public async Task PrivilegedUserMustEnrollOnceTheGracePeriodHasElapsed()
    {
        Guid userId;
        Guid tenantId;
        await using (var setupContext = _fixture.CreateContext())
        {
            var tenant = await CreateTenantAsync(setupContext);
            var role = await CreateGlobalAdminRoleAsync(setupContext, tenant.Id);
            var user = await CreateUserAsync(setupContext);
            user.AssignRole(role);
            setupContext.Users.Update(user);
            await setupContext.SaveChangesAsync();
            userId = user.Id;
            tenantId = tenant.Id;
        }

        // Simulate the grace period having started 15 days ago (past the 14-day window) via a
        // direct SQL update - User.StartMfaGracePeriodIfNotStarted() always stamps "now".
        await using (var backdateContext = _fixture.CreateContext())
        {
            var user = await new UserRepository(backdateContext).GetByIdAsync(userId);
            Assert.NotNull(user);
            typeof(User).GetProperty(nameof(User.MfaGracePeriodStartedAt))!
                .SetValue(user, DateTime.UtcNow.AddDays(-15));
            backdateContext.Users.Update(user);
            await backdateContext.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        var result = await service.MustEnrollMfaBeforeProceedingAsync(userId, tenantId);

        Assert.True(result);
    }

    private sealed class FakeTotpService(bool isEnabled) : ITotpService
    {
        public Task<TotpEnrollment> EnrollAsync(Guid userId, string accountName, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> IsEnabledAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(isEnabled);
        public Task<TotpStatus> GetStatusAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<BackupCodeResult> EnableAsync(Guid userId, string verificationCode, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> VerifyAsync(Guid userId, string code, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<BackupCodeResult> RegenerateBackupCodesAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DisableAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakePushMfaService(bool hasActiveDevices) : IPushMfaService
    {
        public Task<bool> HasActiveDevicesAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(hasActiveDevices);
        public Task<IReadOnlyCollection<PushDeviceDto>> GetDevicesAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RegisterDeviceAsync(Guid userId, string platform, string pushToken, string? deviceName, string? totpCode, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RemoveDeviceAsync(Guid userId, Guid deviceId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PushChallengeCreated> StartChallengeAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ApproveAsync(Guid challengeId, string challengeCode, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> DenyAsync(Guid challengeId, string challengeCode, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<MfaChallengeStatus> GetChallengeStatusAsync(Guid challengeId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> IsChallengeApprovedForUserAsync(Guid challengeId, Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
