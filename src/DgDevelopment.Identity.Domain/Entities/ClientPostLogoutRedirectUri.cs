namespace DgDevelopment.Identity.Domain.Entities;

public sealed class ClientPostLogoutRedirectUri
{
    public Guid ClientId { get; private set; }
    public string RedirectUri { get; private set; }

    private ClientPostLogoutRedirectUri() { }

    public ClientPostLogoutRedirectUri(Guid clientId, string redirectUri)
    {
        ClientId = clientId;
        RedirectUri = redirectUri;
    }
}
