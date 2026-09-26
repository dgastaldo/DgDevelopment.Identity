namespace DgDevelopment.Identity.Server.UnitTests.ValueObjects;

using DgDevelopment.Identity.Domain.ValueObjects;

public sealed class EmailAddressTests
{
    [Theory]
    [InlineData("user@example.com")]
    [InlineData("user.name+tag@example-domain.co")]
    [InlineData("UPPER@example.com")]
    public void IsValidAcceptsWellFormedAddresses(string email)
    {
        Assert.True(EmailAddress.IsValid(email));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("plainaddress")]
    [InlineData("@missing-local")]
    [InlineData("no@domain")]
    [InlineData("a@@b.co")]
    [InlineData("a@b@c.co")]
    public void IsValidRejectsMalformedAddresses(string email)
    {
        Assert.False(EmailAddress.IsValid(email));
    }

    [Fact]
    public void ConstructorNormalizesToUpperInvariant()
    {
        var email = new EmailAddress("User@Example.com");

        Assert.Equal("USER@EXAMPLE.COM", email.Value);
        Assert.Equal("USER@EXAMPLE.COM", email.ToString());
    }

    [Fact]
    public void ConstructorRejectsSurroundingWhitespace()
    {
        Assert.Throws<ArgumentException>(() => new EmailAddress("  User@Example.com  "));
    }

    [Fact]
    public void ConstructorThrowsOnEmptyInput()
    {
        Assert.Throws<ArgumentException>(() => new EmailAddress(""));
        Assert.Throws<ArgumentException>(() => new EmailAddress("   "));
    }

    [Fact]
    public void ConstructorThrowsOnInvalidAddress()
    {
        Assert.Throws<ArgumentException>(() => new EmailAddress("not-an-email"));
    }

    [Fact]
    public void FromStringCreatesInstance()
    {
        var email = EmailAddress.FromString("a@b.co");

        Assert.Equal("A@B.CO", email.Value);
    }
}