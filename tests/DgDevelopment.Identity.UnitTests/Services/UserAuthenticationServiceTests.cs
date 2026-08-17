namespace DgDevelopment.Identity.UnitTests.Services;

using DgDevelopment.Identity.Application.Services;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.UnitTests.Testing;
using Xunit;

public sealed class UserAuthenticationServiceTests : IClassFixture<DatabaseFixture<UserAuthenticationServiceTests>>
{
    private readonly DatabaseFixture<UserAuthenticationServiceTests> _fixture;

    public UserAuthenticationServiceTests(DatabaseFixture<UserAuthenticationServiceTests> fixture)
    {
        _fixture = fixture;
    }

    private static User CreateUser(string password, string? username = null)
    {
        var hasher = new FakePasswordHasher();
        return new User(
            username ?? $"user-{Guid.NewGuid():N}",
            hasher.HashPassword(password),
            EmailAddress.FromString($"{Guid.NewGuid():N}@dgdevelopment.it"));
    }

    private static UserAuthenticationService CreateService(IdentityDbContext context)
        => new(new UserRepository(context), new FakePasswordHasher());

    private async Task<Guid> SeedUserAsync(User user)
    {
        await using var context = _fixture.CreateContext();
        await new UserRepository(context).AddAsync(user);
        return user.Id;
    }

    private async Task<User?> ValidateCredentialsAsync(string username, string password)
    {
        await using var context = _fixture.CreateContext();
        return await CreateService(context).ValidateCredentialsAsync(username, password);
    }

    private async Task<User?> GetStoredUserAsync(Guid userId)
    {
        await using var context = _fixture.CreateContext();
        return await new UserRepository(context).GetByIdAsync(userId);
    }

    [Fact]
    public async Task ValidateCredentialsAsyncReturnsUserOnCorrectPassword()
    {
        var user = CreateUser("P@ssw0rd!");
        await SeedUserAsync(user);

        var result = await ValidateCredentialsAsync(user.Username, "P@ssw0rd!");

        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal(user.Username, result.Username);
    }

    [Fact]
    public async Task ValidateCredentialsAsyncMatchesByEmail()
    {
        var user = CreateUser("P@ssw0rd!");
        await SeedUserAsync(user);

        var result = await ValidateCredentialsAsync(user.PrimaryEmail!.Value, "P@ssw0rd!");

        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
    }

    [Fact]
    public async Task ValidateCredentialsAsyncReturnsNullForUnknownUser()
    {
        var result = await ValidateCredentialsAsync("no-such-user", "P@ssw0rd!");

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateCredentialsAsyncWrongPasswordReturnsNullAndCountsAttempts()
    {
        var user = CreateUser("P@ssw0rd!");
        await SeedUserAsync(user);

        var result = await ValidateCredentialsAsync(user.Username, "wrong-password");

        Assert.Null(result);
        var stored = await GetStoredUserAsync(user.Id);
        Assert.NotNull(stored);
        Assert.Equal(1, stored.FailedLoginAttempts);
    }

    [Fact]
    public async Task ValidateCredentialsAsyncLocksAfterMaxFailedAttempts()
    {
        var user = CreateUser("P@ssw0rd!");
        await SeedUserAsync(user);

        for (var i = 0; i < 5; i++)
        {
            var result = await ValidateCredentialsAsync(user.Username, "wrong-password");
            Assert.Null(result);
        }

        var locked = await GetStoredUserAsync(user.Id);
        Assert.NotNull(locked);
        Assert.True(locked.IsLocked);
        Assert.True(locked.LockoutEnd.HasValue);
    }

    [Fact]
    public async Task ValidateCredentialsAsyncRejectsLockedUserWithinLockout()
    {
        var user = CreateUser("P@ssw0rd!");
        await SeedUserAsync(user);

        for (var i = 0; i < 5; i++)
            await ValidateCredentialsAsync(user.Username, "wrong-password");

        var result = await ValidateCredentialsAsync(user.Username, "P@ssw0rd!");

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateCredentialsAsyncReturnsNullForInactiveUser()
    {
        var user = CreateUser("P@ssw0rd!");
        await using (var context = _fixture.CreateContext())
        {
            var repo = new UserRepository(context);
            await repo.AddAsync(user);
            user.Deactivate();
            await repo.UpdateAsync(user);
        }

        var result = await ValidateCredentialsAsync(user.Username, "P@ssw0rd!");

        Assert.Null(result);
    }

    [Fact]
    public async Task RecordSuccessfulLoginResetsFailedAttempts()
    {
        var user = CreateUser("P@ssw0rd!");
        await SeedUserAsync(user);

        for (var i = 0; i < 3; i++)
        {
            await using var context = _fixture.CreateContext();
            var service = CreateService(context);
            var stored = await new UserRepository(context).GetByIdAsync(user.Id);
            Assert.NotNull(stored);
            await service.RecordFailedLoginAsync(stored);
        }

        await using (var successContext = _fixture.CreateContext())
        {
            var stored = await new UserRepository(successContext).GetByIdAsync(user.Id);
            Assert.NotNull(stored);
            await CreateService(successContext).RecordSuccessfulLoginAsync(stored);
        }

        var after = await GetStoredUserAsync(user.Id);
        Assert.NotNull(after);
        Assert.Equal(0, after.FailedLoginAttempts);
        Assert.False(after.IsLocked);
    }

    [Fact]
    public async Task RecordFailedLoginLocksWhenThresholdReached()
    {
        var user = CreateUser("P@ssw0rd!");
        await SeedUserAsync(user);

        for (var i = 0; i < 5; i++)
        {
            await using var context = _fixture.CreateContext();
            var service = CreateService(context);
            var stored = await new UserRepository(context).GetByIdAsync(user.Id);
            Assert.NotNull(stored);
            await service.RecordFailedLoginAsync(stored);
        }

        var locked = await GetStoredUserAsync(user.Id);
        Assert.NotNull(locked);
        Assert.True(locked.IsLocked);
        Assert.Equal(5, locked.FailedLoginAttempts);
    }

    [Fact]
    public async Task FindByIdentifierAsyncFindsByUsernameAndEmail()
    {
        var user = CreateUser("P@ssw0rd!");
        await SeedUserAsync(user);
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        var byUsername = await service.FindByIdentifierAsync(user.Username);
        var byEmail = await service.FindByIdentifierAsync(user.PrimaryEmail!.Value);

        Assert.Equal(user.Id, byUsername?.Id);
        Assert.Equal(user.Id, byEmail?.Id);
    }

    [Fact]
    public async Task FindByIdentifierAsyncReturnsNullForUnknown()
    {
        await using var context = _fixture.CreateContext();
        var result = await CreateService(context).FindByIdentifierAsync("no-such-user");

        Assert.Null(result);
    }
}