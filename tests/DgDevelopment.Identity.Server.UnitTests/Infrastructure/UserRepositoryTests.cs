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

    // Each mutation below uses its OWN fresh context, matching how a real HTTP request always
    // gets a brand-new, per-request DbContext - reusing one context across AddAsync and a later
    // mutation (as other tests in this file do for the independent join-table methods) makes the
    // owned Emails collection's re-query in these methods collide with the already-tracked graph
    // from the earlier AddAsync, since EF's identity resolution and the Include query's owned
    // materialization don't reconcile cleanly for an entity that's already tracked.

    [Fact]
    public async Task AddEmailAsyncPersistsANewEmail()
    {
        var (username, email) = Unique();
        var user = CreateUser(username, email);
        await using (var seedContext = _fixture.CreateContext())
            await new UserRepository(seedContext).AddAsync(user);

        var secondEmail = EmailAddress.FromString($"second-{Guid.NewGuid():N}@dgdevelopment.it");
        await using (var context = _fixture.CreateContext())
            await new UserRepository(context).AddEmailAsync(user.Id, secondEmail, isPrimary: false);

        await using var readContext = _fixture.CreateContext();
        var stored = await new UserRepository(readContext).GetByIdAsync(user.Id);
        Assert.Equal(2, stored!.Emails.Count);
        var added = Assert.Single(stored.Emails, e => e.Email.Value == secondEmail.Value);
        Assert.False(added.IsPrimary);
    }

    [Fact]
    public async Task RemoveEmailAsyncRemovesANonPrimaryEmail()
    {
        var (username, email) = Unique();
        var user = CreateUser(username, email);
        await using (var seedContext = _fixture.CreateContext())
            await new UserRepository(seedContext).AddAsync(user);

        var secondEmail = EmailAddress.FromString($"second-{Guid.NewGuid():N}@dgdevelopment.it");
        await using (var addContext = _fixture.CreateContext())
            await new UserRepository(addContext).AddEmailAsync(user.Id, secondEmail, isPrimary: false);

        await using (var removeContext = _fixture.CreateContext())
            await new UserRepository(removeContext).RemoveEmailAsync(user.Id, secondEmail);

        await using var readContext = _fixture.CreateContext();
        var stored = await new UserRepository(readContext).GetByIdAsync(user.Id);
        Assert.Single(stored!.Emails);
    }

    [Fact]
    public async Task RemoveEmailAsyncThrowsWhenRemovingTheSolePrimaryEmail()
    {
        var (username, email) = Unique();
        var user = CreateUser(username, email);
        await using var context = _fixture.CreateContext();
        var repo = new UserRepository(context);
        await repo.AddAsync(user);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repo.RemoveEmailAsync(user.Id, EmailAddress.FromString(email)));

        var stored = await repo.GetByIdAsync(user.Id);
        Assert.Single(stored!.Emails);
    }

    [Fact]
    public async Task SetPrimaryEmailAsyncChangesThePrimaryEmail()
    {
        var (username, email) = Unique();
        var user = CreateUser(username, email);
        await using (var seedContext = _fixture.CreateContext())
            await new UserRepository(seedContext).AddAsync(user);

        var secondEmail = EmailAddress.FromString($"second-{Guid.NewGuid():N}@dgdevelopment.it");
        await using (var addContext = _fixture.CreateContext())
            await new UserRepository(addContext).AddEmailAsync(user.Id, secondEmail, isPrimary: false);

        await using (var primaryContext = _fixture.CreateContext())
            await new UserRepository(primaryContext).SetPrimaryEmailAsync(user.Id, secondEmail);

        await using var readContext = _fixture.CreateContext();
        var stored = await new UserRepository(readContext).GetByIdAsync(user.Id);
        Assert.Equal(secondEmail.Value, stored!.PrimaryEmail!.Value);
    }

    [Fact]
    public async Task VerifyEmailAsyncMarksTheEmailVerified()
    {
        var (username, email) = Unique();
        var user = CreateUser(username, email);
        await using var context = _fixture.CreateContext();
        var repo = new UserRepository(context);
        await repo.AddAsync(user);

        await repo.VerifyEmailAsync(user.Id, EmailAddress.FromString(email));

        var stored = await repo.GetByIdAsync(user.Id);
        Assert.True(stored!.Emails.Single().IsVerified);
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