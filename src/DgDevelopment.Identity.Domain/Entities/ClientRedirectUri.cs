namespace DgDevelopment.Identity.Domain.Entities;

public sealed class ClientRedirectUri
{
    public Guid ClientId { get; private set; }
    public string RedirectUri { get; private set; }

    private ClientRedirectUri() { }

    public ClientRedirectUri(Guid clientId, string redirectUri)
    {
        ClientId = clientId;
        RedirectUri = redirectUri;
    }
}
