namespace DgDevelopment.Identity.Domain.Entities;

public sealed class ClientAdminConsent
{
    public Guid ClientId { get; private set; }
    public string Scope { get; private set; }

    private ClientAdminConsent() { }

    public ClientAdminConsent(Guid clientId, string scope)
    {
        ClientId = clientId;
        Scope = scope;
    }
}