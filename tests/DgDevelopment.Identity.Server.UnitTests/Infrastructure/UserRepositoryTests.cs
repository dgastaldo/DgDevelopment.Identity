namespace DgDevelopment.Identity.Server.UnitTests.Infrastructure;

using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Xunit;

public sealed class UserRepositoryTests : IClassFixture<DatabaseFixture<UserRepositoryTests>>
{
    private readonly DatabaseFixture<UserRepositoryTests> _fixture;

    public UserRepositoryTests(DatabaseFixture<UserRepositoryTests> fixture)
    {
        _fixture = fixture;
    }

    private static (string Username, string Email) Unique()
        => ($"user-{Guid.NewGuid():N}", $"{Guid.NewGuid():N}@dgdevelopment.it");

    private static User CreateUser(string username, string email)
        => new(username, "hash", EmailAddress.FromString(email));

    [Fact]
    public async Task AddAndGetByIdRoundTrips()
    {
        var (username, email) = Unique();
        var user = CreateUser(username, email);
        await using var context = _fixture.CreateContext();
        var repo = new UserRepository(context);
        await repo.AddAsync(user);

        var stored = await repo.GetByIdAsync(user.Id);

        Assert.NotNull(stored);
        Assert.Equal(user.Id, stored.Id);
        Assert.Equal(username, stored.Username);
        Assert.True(stored.IsActive);
        Assert.Equal(email.ToUpperInvariant(), stored.PrimaryEmail!.Value);
    }

    [Fact]
    public async Task GetByUsernameRoundTrips()
    {
        var (username, email) = Unique();
        var user = CreateUser(username, email);
        await using var context = _fixture.CreateContext();
        var repo = new UserRepository(context);
        await repo.AddAsync(user);

        var stored = await repo.GetByUsernameAsync(username);

        Assert.NotNull(stored);
        Assert.Equal(user.Id, stored.Id);
    }

    [Fact]
    public async Task GetByUsernameReturnsNullForUnknownUser()
    {
        await using var context = _fixture.CreateContext();
        var repo = new UserRepository(context);

        var stored = await repo.GetByUsernameAsync($"missing-{Guid.NewGuid():N}");

        Assert.Null(stored);
    }

    [Fact]
    public async Task GetByEmailRoundTripsIgnoringCase()
    {
        var (_, email) = Unique();
        var user = CreateUser($"user-{Guid.NewGuid():N}", email);
        await using var context = _fixture.CreateContext();
        var repo = new UserRepository(context);
        await repo.AddAsync(user);

        var stored = await repo.GetByEmailAsync(email.ToUpperInvariant());

        Assert.NotNull(stored);
        Assert.Equal(user.Id, stored.Id);
    }

    [Fact]
    public async Task GetByEmailReturnsNullForUnknownEmail()
    {
        await using var context = _fixture.CreateContext();
        var repo = new UserRepository(context);

        var stored = await repo.GetByEmailAsync($"{Guid.NewGuid():N}@unknown.example");

        Assert.Null(stored);
    }

    [Fact]
    public async Task GetByLoginRoundTrips()
    {
        var (username, email) = Unique();
        var user = CreateUser(username, email);
        user.AddLogin("github", $"key-{Guid.NewGuid():N}", "gh-user");
        await using var context = _fixture.CreateContext();
        var repo = new UserRepository(context);
        await repo.AddAsync(user);

        var login = user.Logins.Single();
        var stored = await repo.GetByLoginAsync(login.Provider, login.ProviderKey);

        Assert.NotNull(stored);
        Assert.Equal(user.Id, stored.Id);
    }

    [Fact]
    public async Task GetByLoginReturnsNullForUnknownLogin()
    {
        await using var context = _fixture.CreateContext();
        var repo = new UserRepository(context);

        var stored = await repo.GetByLoginAsync("github", "unknown-key");

        Assert.Null(stored);
    }

    [Fact]
    public async Task UpdateAsyncPersistsChanges()
    {
        var (username, email) = Unique();
        var user = CreateUser(username, email);
        await using var context = _fixture.CreateContext();
        var repo = new UserRepository(context);
        await repo.AddAsync(user);
        user.Deactivate();
        await repo.UpdateAsync(user);

        var stored = await repo.GetByIdAsync(user.Id);

        Assert.NotNull(stored);
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task DeleteAsyncRemovesUser()
    {
        var (username, email) = Unique();
        var user = CreateUser(username, email);
        await using var context = _fixture.CreateContext();
        var repo = new UserRepository(context);
        await repo.AddAsync(user);

        await repo.DeleteAsync(user.Id);

        Assert.Null(await repo.GetByIdAsync(user.Id));
    }
}