namespace DgDevelopment.Identity.Server.UnitTests.Domain;

using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;
using Xunit;

public sealed class UserTests
{
    private static readonly EmailAddress Email = EmailAddress.FromString("user@example.com");
    private static readonly Guid TenantId = Guid.NewGuid();

    private static User CreateUser() => new($"user-{Guid.NewGuid():N}", "hash", Email);

    [Fact]
    public void ConstructorCreatesActiveUserWithPrimaryEmail()
    {
        var user = CreateUser();

        Assert.True(user.IsActive);
        Assert.False(user.IsLocked);
        Assert.False(user.IsSystemAccount);
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Equal(Email.Value, user.PrimaryEmail!.Value);
    }

    [Fact]
    public void ConstructorSetsSystemAccountFlag()
    {
        var user = new User("sys", "hash", Email, isSystemAccount: true);

        Assert.True(user.IsSystemAccount);
    }

    [Fact]
    public void AddEmailAddsNonPrimaryEmail()
    {
        var user = CreateUser();
        var second = EmailAddress.FromString("second@example.com");

        user.AddEmail(second);

        Assert.Equal(2, user.Emails.Count);
        Assert.Equal(Email.Value, user.PrimaryEmail!.Value);
    }

    [Fact]
    public void AddEmailThrowsForDuplicateEmail()
    {
        var user = CreateUser();

        Assert.Throws<InvalidOperationException>(() => user.AddEmail(Email));
    }

    [Fact]
    public void AddEmailAsPrimarySwitchesPrimaryEmail()
    {
        var user = CreateUser();
        var second = EmailAddress.FromString("second@example.com");

        user.AddEmail(second, isPrimary: true);

        Assert.Equal(second.Value, user.PrimaryEmail!.Value);
        Assert.Single(user.Emails, e => e.IsPrimary);
    }

    [Fact]
    public void SetPrimaryEmailSwitchesPrimary()
    {
        var user = CreateUser();
        var second = EmailAddress.FromString("second@example.com");
        user.AddEmail(second);

        user.SetPrimaryEmail(second);

        Assert.Equal(second.Value, user.PrimaryEmail!.Value);
        Assert.Single(user.Emails, e => e.IsPrimary);
    }

    [Fact]
    public void SetPrimaryEmailThrowsForUnknownEmail()
    {
        var user = CreateUser();

        Assert.Throws<InvalidOperationException>(() => user.SetPrimaryEmail(EmailAddress.FromString("unknown@example.com")));
    }

    [Fact]
    public void RemoveEmailRemovesSecondaryEmail()
    {
        var user = CreateUser();
        var second = EmailAddress.FromString("second@example.com");
        user.AddEmail(second);

        user.RemoveEmail(second);

        Assert.Single(user.Emails);
    }

    [Fact]
    public void RemoveEmailThrowsWhenRemovingOnlyEmail()
    {
        var user = CreateUser();

        Assert.Throws<InvalidOperationException>(() => user.RemoveEmail(Email));
    }

    [Fact]
    public void RemoveEmailThrowsForUnknownEmail()
    {
        var user = CreateUser();

        Assert.Throws<InvalidOperationException>(() => user.RemoveEmail(EmailAddress.FromString("unknown@example.com")));
    }

    [Fact]
    public void VerifyEmailMarksEmailVerified()
    {
        var user = CreateUser();

        user.VerifyEmail(Email);

        var email = Assert.Single(user.Emails);
        Assert.True(email.IsVerified);
        Assert.NotNull(email.VerifiedAt);
    }

    [Fact]
    public void VerifyEmailThrowsForUnknownEmail()
    {
        var user = CreateUser();

        Assert.Throws<InvalidOperationException>(() => user.VerifyEmail(EmailAddress.FromString("unknown@example.com")));
    }

    [Fact]
    public void AddAndRemoveClaimRoundTrip()
    {
        var user = CreateUser();
        user.AddClaim("permission", "user:read");

        var claim = Assert.Single(user.Claims);
        Assert.Equal("permission", claim.Type);
        Assert.Equal("user:read", claim.Value);

        user.RemoveClaim("permission");

        Assert.Empty(user.Claims);
    }

    [Fact]
    public void RemoveClaimRemovesOnlyMatchingType()
    {
        var user = CreateUser();
        user.AddClaim("a", "1");
        user.AddClaim("b", "2");

        user.RemoveClaim("a");

        Assert.Single(user.Claims);
        Assert.Equal("b", Assert.Single(user.Claims).Type);
    }

    [Fact]
    public void AddAndRemoveLoginRoundTrip()
    {
        var user = CreateUser();
        user.AddLogin("github", "key-123", "gh-123");

        var login = Assert.Single(user.Logins);
        Assert.Equal("github", login.Provider);
        Assert.Equal("key-123", login.ProviderKey);
        Assert.Equal("gh-123", login.ProviderDisplayName);

        user.RemoveLogin("github", "key-123");

        Assert.Empty(user.Logins);
    }

    [Fact]
    public void SetPasswordUpdatesHash()
    {
        var user = CreateUser();

        user.SetPassword("new-hash");

        Assert.Equal("new-hash", user.PasswordHash);
    }

    [Fact]
    public void LockSetsLockoutAndUnlockRestores()
    {
        var user = CreateUser();

        user.Lock();
        Assert.True(user.IsLocked);
        Assert.NotNull(user.LockoutEnd);

        user.Unlock();
        Assert.False(user.IsLocked);
        Assert.Null(user.LockoutEnd);
        Assert.Equal(0, user.FailedLoginAttempts);
    }

    [Fact]
    public void LockUsesProvidedLockoutEnd()
    {
        var user = CreateUser();
        var lockoutEnd = DateTime.UtcNow.AddDays(1);

        user.Lock(lockoutEnd);

        Assert.Equal(lockoutEnd, user.LockoutEnd);
    }

    [Fact]
    public void RecordFailedLoginIncrementsCounter()
    {
        var user = CreateUser();

        user.RecordFailedLogin();
        user.RecordFailedLogin();

        Assert.Equal(2, user.FailedLoginAttempts);
    }

    [Fact]
    public void DeactivateAndActivateToggle()
    {
        var user = CreateUser();

        user.Deactivate();
        Assert.False(user.IsActive);

        user.Activate();
        Assert.True(user.IsActive);
    }

    [Fact]
    public void AssignRoleAddsOnceAndRemoveRoleClears()
    {
        var user = CreateUser();
        var role = new Role(TenantId, Guid.NewGuid(), "Tester", "desc");

        user.AssignRole(role);
        user.AssignRole(role);

        Assert.Single(user.Roles);
        Assert.Equal(role.Id, Assert.Single(user.Roles).RoleId);

        user.RemoveRole(role.Id);
        Assert.Empty(user.Roles);
    }

    [Fact]
    public void GrantPermissionAddsOnceAndRevokeClears()
    {
        var user = CreateUser();
        var permission = new Permission(TenantId, Guid.NewGuid(), "role:read", "desc", "Role");

        user.GrantPermission(TenantId, permission);
        user.GrantPermission(TenantId, permission);

        Assert.Single(user.Permissions);
        Assert.Equal(permission.Id, Assert.Single(user.Permissions).PermissionId);

        user.RevokePermission(permission.Id);
        Assert.Empty(user.Permissions);
    }

    [Fact]
    public void AddToGroupAddsOnceAndRemoveFromGroupClears()
    {
        var user = CreateUser();
        var group = new Group(TenantId, "G", "desc");

        user.AddToGroup(group);
        user.AddToGroup(group);

        Assert.Single(user.Groups);
        Assert.Equal(group.Id, Assert.Single(user.Groups).GroupId);

        user.RemoveFromGroup(group.Id);
        Assert.Empty(user.Groups);
    }
}