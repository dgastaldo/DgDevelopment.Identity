using DgDevelopment.Identity.Application.Authorization;
using DgDevelopment.Identity.Domain.Authorization;
using DgDevelopment.Identity.Domain.Repositories;

namespace DgDevelopment.Identity.Server.UnitTests.Application;

public sealed class EffectivePermissionsServiceTests
{
    [Fact]
    public async Task HasPermissionAsyncMatchesUnscopedGrant()
    {
        var permission = new EffectivePermission("identity-platform.user.read", null, null);
        var service = new EffectivePermissionsService(new StubRepository(permission));

        Assert.True(await service.HasPermissionAsync(Guid.NewGuid(), permission.Name));
    }

    [Fact]
    public async Task HasPermissionAsyncMatchesRequestedScope()
    {
        var permission = new EffectivePermission("identity-platform.user.read", "department", "finance");
        var service = new EffectivePermissionsService(new StubRepository(permission));

        Assert.True(await service.HasPermissionAsync(Guid.NewGuid(), permission.Name, "department", "finance"));
        Assert.False(await service.HasPermissionAsync(Guid.NewGuid(), permission.Name, "department", "sales"));
    }

    [Fact]
    public async Task HasPermissionAsyncRejectsDifferentPermission()
    {
        var service = new EffectivePermissionsService(new StubRepository(
            new EffectivePermission("identity-platform.user.read", null, null)));

        Assert.False(await service.HasPermissionAsync(Guid.NewGuid(), "identity-platform.user.update"));
    }

    private sealed class StubRepository(params EffectivePermission[] permissions) : IUserAuthorizationRepository
    {
        public Task<IReadOnlyCollection<EffectivePermission>> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyCollection<EffectivePermission>>(permissions);
    }
}
