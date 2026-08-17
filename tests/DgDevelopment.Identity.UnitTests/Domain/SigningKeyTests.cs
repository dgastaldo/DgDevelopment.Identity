namespace DgDevelopment.Identity.UnitTests.Domain;

using DgDevelopment.Identity.Domain.Entities;
using Xunit;

public sealed class SigningKeyTests
{
    [Fact]
    public void ConstructorSetsFields()
    {
        var key = new SigningKey("kid-1", "RS256", "private-pem", "public-pem", DateTime.UtcNow.AddDays(90));

        Assert.Equal("kid-1", key.Id);
        Assert.Equal("RS256", key.Algorithm);
        Assert.Equal("private-pem", key.KeyData);
        Assert.Equal("public-pem", key.PublicKeyData);
        Assert.True(key.IsActive);
        Assert.True(key.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void IsExpiredReturnsTrueForPastExpiry()
    {
        var key = new SigningKey("kid-1", "RS256", "private", "public", DateTime.UtcNow.AddSeconds(-1));

        Assert.True(key.IsExpired());
    }

    [Fact]
    public void IsExpiredReturnsFalseForFutureExpiry()
    {
        var key = new SigningKey("kid-1", "RS256", "private", "public", DateTime.UtcNow.AddDays(90));

        Assert.False(key.IsExpired());
    }

    [Fact]
    public void DeactivateSetsInactive()
    {
        var key = new SigningKey("kid-1", "RS256", "private", "public", DateTime.UtcNow.AddDays(90));

        key.Deactivate();

        Assert.False(key.IsActive);
        Assert.False(key.IsExpired());
    }
}