using System.Text.RegularExpressions;

namespace DgDevelopment.Identity.Domain.ValueObjects;

public sealed record EmailAddress
{
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
        // No backtracking blowup possible (no nested quantifiers over the same class), but S6444
        // wants an explicit timeout regardless of whether one could ever actually trigger.
        return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
    }

    public override string ToString() => Value;

    public static explicit operator EmailAddress(string value) => new(value);

    public static EmailAddress FromString(string value) => new(value);
}
