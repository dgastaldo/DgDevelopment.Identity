namespace DgDevelopment.Identity.UnitTests.Infrastructure;

using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.UnitTests.Testing;
using Xunit;

public sealed class PlatformRepositoryTests : IClassFixture<DatabaseFixture<PlatformRepositoryTests>>
{
    private readonly DatabaseFixture<PlatformRepositoryTests> _fixture;

    public PlatformRepositoryTests(DatabaseFixture<PlatformRepositoryTests> fixture)
    {
        _fixture = fixture;
    }

    private static string UniqueName() => $"platform-{Guid.NewGuid():N}";

    [Fact]
    public async Task AddAndGetByIdRoundTrips()
    {
        var name = UniqueName();
        var platform = new Platform(name, "desc", PermissionMode.AuthOnly);
        await using var context = _fixture.CreateContext();
        var repo = new PlatformRepository(context);
        await repo.AddAsync(platform);

        var stored = await repo.GetByIdAsync(platform.Id);

        Assert.NotNull(stored);
        Assert.Equal(platform.Id, stored.Id);
        Assert.Equal(name, stored.Name);
        Assert.Equal("desc", stored.Description);
        Assert.Equal(PermissionMode.AuthOnly, stored.PermissionMode);
    }

    [Fact]
    public async Task GetAllReturnsSeededAndNewPlatforms()
    {
        await using var context = _fixture.CreateContext();
        var repo = new PlatformRepository(context);
        await repo.AddAsync(new Platform(UniqueName(), "desc", PermissionMode.IdentityManaged));

        var stored = await repo.GetAllAsync();

        Assert.Contains(stored, p => p.Name == "IdentityAdmin");
    }

    [Fact]
    public async Task UpdateAsyncDoesNotRemoveExistingData()
    {
        var name = UniqueName();
        var platform = new Platform(name, "desc", PermissionMode.AuthOnly);
        await using var context = _fixture.CreateContext();
        var repo = new PlatformRepository(context);
        await repo.AddAsync(platform);

        await repo.UpdateAsync(platform);

        var stored = await repo.GetByIdAsync(platform.Id);
        Assert.NotNull(stored);
        Assert.Equal(name, stored.Name);
    }

    [Fact]
    public async Task DeleteAsyncRemovesPlatform()
    {
        var name = UniqueName();
        var platform = new Platform(name, "desc", PermissionMode.AuthOnly);
        await using var context = _fixture.CreateContext();
        var repo = new PlatformRepository(context);
        await repo.AddAsync(platform);

        await repo.DeleteAsync(platform.Id);

        var stored = await repo.GetAllAsync();
        Assert.DoesNotContain(stored, p => p.Id == platform.Id);
    }
}