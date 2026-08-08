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

        Value = value.ToLowerInvariant().Trim();
    }

    public static bool IsValid(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
    }

    public override string ToString() => Value;

    public static implicit operator string(EmailAddress email) => email.Value;
    public static explicit operator EmailAddress(string value) => new(value);
}
