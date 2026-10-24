namespace DgDevelopment.Identity.Domain.Entities;

public sealed class UserClaim
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Type { get; private set; }
    public string Value { get; private set; }

    private UserClaim() { }

    public UserClaim(Guid userId, string type, string value)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Type = type;
        Value = value;
    }
}
