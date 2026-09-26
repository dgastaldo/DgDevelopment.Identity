using System;

namespace DgDevelopment.Identity.Domain.Entities;

public sealed class ClientPostLogoutRedirectUri
{
    public Guid ClientId { get; private set; }
    public Uri RedirectUri { get; private set; }

    private ClientPostLogoutRedirectUri() { }

    public ClientPostLogoutRedirectUri(Guid clientId, Uri redirectUri)
    {
        ArgumentNullException.ThrowIfNull(redirectUri);
        ClientId = clientId;
        RedirectUri = redirectUri;
    }
}
