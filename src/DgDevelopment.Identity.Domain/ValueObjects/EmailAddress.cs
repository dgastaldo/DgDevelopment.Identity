using System.Text.RegularExpressions;

namespace DgDevelopment.Identity.Domain.ValueObjects;

public sealed record EmailAddress
{
    // The pattern itself has no backtracking blowup (no nested quantifiers over the same class),
    // but a timeout is cheap insurance against a future edit introducing one - and silences
    // S6444 either way.
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    public string Value { get; }

    public EmailAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Email address cannot be empty.", nameof(value));
        if (!IsValid(value))
            throw new ArgumentException($"Invalid email address: {value}", nameof(value));

        Value = value.ToUpperInvariant().Trim();
    }

    public static bool IsValid(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        try
        {
            return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase, RegexTimeout);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    public override string ToString() => Value;

    public static explicit operator EmailAddress(string value) => new(value);

    public static EmailAddress FromString(string value) => new(value);
}
