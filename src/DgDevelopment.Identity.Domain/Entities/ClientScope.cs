namespace DgDevelopment.Identity.Domain.Entities;

public sealed class ClientScope
{
    public Guid ClientId { get; private set; }
    public string Scope { get; private set; }

    private ClientScope() { }

    public ClientScope(Guid clientId, string scope)
    {
        ClientId = clientId;
        Scope = scope;
    }
}
