namespace DgDevelopment.Identity.Domain.ValueObjects;

public sealed record ClientId
{
    public string Value { get; }

    public ClientId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200)
            throw new ArgumentException("Client ID must be between 1 and 200 characters.", nameof(value));
        Value = value;
    }

    public override string ToString() => Value;
}
