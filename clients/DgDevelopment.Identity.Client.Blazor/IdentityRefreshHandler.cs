namespace DgDevelopment.Identity.Client.Blazor;

public sealed class IdentityRefreshHandler : DelegatingHandler
{
    private readonly ITokenStore _tokenStore;
    private readonly Func<IdentityAuthStateProvider> _authStateFactory;

    public IdentityRefreshHandler(ITokenStore tokenStore, Func<IdentityAuthStateProvider> authStateFactory)
    {
        _tokenStore = tokenStore;
        _authStateFactory = authStateFactory;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tokens = await _tokenStore.GetTokensAsync().ConfigureAwait(false);

        if (tokens != null && !string.IsNullOrEmpty(tokens.AccessToken))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && tokens?.RefreshToken != null)
        {
            await _authStateFactory().LogoutAsync().ConfigureAwait(false);
        }

        return response;
    }
}
