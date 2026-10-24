namespace DgDevelopment.Identity.Server.UnitTests.Security;

using DgDevelopment.Identity.Infrastructure.Security;

public sealed class Argon2PasswordHasherTests
{
    private readonly Argon2PasswordHasher _hasher = new();

    [Fact]
    public void VerifyPasswordAcceptsRoundTrippedPassword()
    {
        var hash = _hasher.HashPassword("SuperSecretPassword");

        Assert.True(_hasher.VerifyPassword("SuperSecretPassword", hash));
    }

    [Fact]
    public void VerifyPasswordRejectsWrongPassword()
    {
        var hash = _hasher.HashPassword("Correct-Horse-Battery-Staple");

        Assert.False(_hasher.VerifyPassword("wrong-password", hash));
    }

    [Fact]
    public void HashPasswordProducesDistinctHashesForSamePassword()
    {
        var first = _hasher.HashPassword("same-password");
        var second = _hasher.HashPassword("same-password");

        Assert.NotEqual(first, second);
        Assert.True(_hasher.VerifyPassword("same-password", first));
        Assert.True(_hasher.VerifyPassword("same-password", second));
    }

    [Fact]
    public void VerifyPasswordThrowsOnMalformedHash()
    {
        Assert.Throws<FormatException>(() => _hasher.VerifyPassword("password", "not-base64!!!"));
    }
}