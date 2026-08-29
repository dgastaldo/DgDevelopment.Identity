using DgDevelopment.Identity.Application.Common;

namespace DgDevelopment.Identity.Server.UnitTests.Application;

public sealed class PasswordPolicyTests
{
    [Fact]
    public void ValidateAcceptsAPasswordMeetingEveryRule()
    {
        var exception = Record.Exception(() => PasswordPolicy.Validate("Correct-Horse-1"));

        Assert.Null(exception);
    }

    [Fact]
    public void ValidateRejectsAPasswordWithNoUppercaseLetter()
    {
        Assert.Throws<ArgumentException>(() => PasswordPolicy.Validate("no-uppercase-1"));
    }

    [Fact]
    public void ValidateRejectsAPasswordWithNoLowercaseLetter()
    {
        Assert.Throws<ArgumentException>(() => PasswordPolicy.Validate("NO-LOWERCASE-1"));
    }

    [Fact]
    public void ValidateRejectsAPasswordWithNoSpecialCharacter()
    {
        Assert.Throws<ArgumentException>(() => PasswordPolicy.Validate("NoSpecialChar1"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateRejectsNullOrWhitespace(string? password)
    {
        // ThrowIfNullOrWhiteSpace throws ArgumentNullException for null specifically (a subtype
        // of ArgumentException) - ThrowsAny accepts either, unlike Throws's exact-type match.
        Assert.ThrowsAny<ArgumentException>(() => PasswordPolicy.Validate(password!));
    }

    [Fact]
    public void ValidateRejectsAPasswordShorterThanTheMinimumLength()
    {
        Assert.Throws<ArgumentException>(() => PasswordPolicy.Validate("Ab1"));
    }

    [Fact]
    public void ValidateRejectsAPasswordWithNoLetter()
    {
        Assert.Throws<ArgumentException>(() => PasswordPolicy.Validate("1234567890"));
    }

    [Fact]
    public void ValidateRejectsAPasswordWithNoDigit()
    {
        Assert.Throws<ArgumentException>(() => PasswordPolicy.Validate("NoDigitsHere"));
    }
}
