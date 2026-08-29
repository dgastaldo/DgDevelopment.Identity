namespace DgDevelopment.Identity.Server.UnitTests.Application;

using DgDevelopment.Identity.Application.Common;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Xunit;

public sealed class PasswordHistoryServiceTests : IClassFixture<DatabaseFixture<PasswordHistoryServiceTests>>
{
    private readonly DatabaseFixture<PasswordHistoryServiceTests> _fixture;

    public PasswordHistoryServiceTests(DatabaseFixture<PasswordHistoryServiceTests> fixture)
    {
        _fixture = fixture;
    }

    private static PasswordHistoryService CreateService(IdentityDbContext context)
        => new(new PasswordHistoryRepository(context), new FakePasswordHasher());

    private static async Task<User> CreateUserAsync(IdentityDbContext context, string passwordHash)
    {
        var user = new User($"pwdhist-{Guid.NewGuid():N}", passwordHash, EmailAddress.FromString($"{Guid.NewGuid():N}@dgdevelopment.it"));
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task EnsureNotReusedAsyncRejectsTheCurrentPassword()
    {
        await using var context = _fixture.CreateContext();
        var hasher = new FakePasswordHasher();
        var user = await CreateUserAsync(context, hasher.HashPassword("Current-Password-1!"));
        var service = CreateService(context);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.EnsureNotReusedAsync(user.Id, "Current-Password-1!", user.PasswordHash));
    }

    [Fact]
    public async Task EnsureNotReusedAsyncRejectsARecentlyRecordedPassword()
    {
        await using var context = _fixture.CreateContext();
        var hasher = new FakePasswordHasher();
        var user = await CreateUserAsync(context, hasher.HashPassword("Current-Password-1!"));
        var service = CreateService(context);

        await service.RecordChangeAsync(user.Id, hasher.HashPassword("Old-Password-1!"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.EnsureNotReusedAsync(user.Id, "Old-Password-1!", user.PasswordHash));
    }

    [Fact]
    public async Task EnsureNotReusedAsyncAllowsAGenuinelyNewPassword()
    {
        await using var context = _fixture.CreateContext();
        var hasher = new FakePasswordHasher();
        var user = await CreateUserAsync(context, hasher.HashPassword("Current-Password-1!"));
        var service = CreateService(context);

        var exception = await Record.ExceptionAsync(() =>
            service.EnsureNotReusedAsync(user.Id, "Brand-New-Password-1!", user.PasswordHash));

        Assert.Null(exception);
    }

    [Fact]
    public async Task RecordChangeAsyncPrunesHistoryBeyondTheRememberedCount()
    {
        await using var context = _fixture.CreateContext();
        var hasher = new FakePasswordHasher();
        var user = await CreateUserAsync(context, hasher.HashPassword("Current-Password-1!"));
        var repository = new PasswordHistoryRepository(context);
        var service = CreateService(context);

        // RememberedPasswordCount is 5 (current + 4 history rows) - recording 6 changes should
        // leave only the 4 most recent in history.
        for (var i = 0; i < 6; i++)
            await service.RecordChangeAsync(user.Id, hasher.HashPassword($"Password-{i}-1!"));

        var history = await repository.GetRecentAsync(user.Id, take: 10);
        Assert.Equal(4, history.Count);
    }
}
