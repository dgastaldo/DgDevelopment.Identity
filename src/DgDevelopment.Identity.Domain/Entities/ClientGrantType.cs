namespace DgDevelopment.Identity.Domain.Entities;

public sealed class ClientGrantType
{
    public Guid ClientId { get; private set; }
    public string GrantType { get; private set; }

    private ClientGrantType() { }

    public ClientGrantType(Guid clientId, string grantType)
    {
        ClientId = clientId;
        GrantType = grantType;
    }
}
