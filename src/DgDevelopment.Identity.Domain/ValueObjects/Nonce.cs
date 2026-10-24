namespace DgDevelopment.Identity.Domain.ValueObjects;

public sealed record Nonce
{
    public string Value { get; }

    public Nonce(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 512)
            throw new ArgumentException("Nonce must be between 1 and 512 characters.", nameof(value));
        Value = value;
    }

    public static Nonce Generate() => new(Guid.NewGuid().ToString("N"));

    public override string ToString() => Value;
}
