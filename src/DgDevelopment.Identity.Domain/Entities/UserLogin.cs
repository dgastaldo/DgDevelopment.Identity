namespace DgDevelopment.Identity.Domain.Entities;

public sealed class UserLogin
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Provider { get; private set; }
    public string ProviderKey { get; private set; }
    public string? ProviderDisplayName { get; private set; }

    private UserLogin() { }

    public UserLogin(Guid userId, string provider, string providerKey, string? providerDisplayName = null)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Provider = provider;
        ProviderKey = providerKey;
        ProviderDisplayName = providerDisplayName;
    }
}
