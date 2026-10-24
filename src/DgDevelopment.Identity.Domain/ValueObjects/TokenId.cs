namespace DgDevelopment.Identity.Domain.ValueObjects;

public sealed record TokenId
{
    public string Value { get; }

    public TokenId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Token ID cannot be empty.", nameof(value));
        Value = value;
    }

    public static TokenId New() => new(Guid.NewGuid().ToString("N"));

    public override string ToString() => Value;
}
