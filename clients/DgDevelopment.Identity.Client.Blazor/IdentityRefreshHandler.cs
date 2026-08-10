namespace DgDevelopment.Identity.Client.Blazor;

public sealed class IdentityRefreshHandler : DelegatingHandler
{
    private readonly ITokenStore _tokenStore;
    private readonly IdentityAuthStateProvider _authState;

    public IdentityRefreshHandler(ITokenStore tokenStore, IdentityAuthStateProvider authState)
    {
        _tokenStore = tokenStore;
        _authState = authState;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var tokens = await _tokenStore.GetTokensAsync().ConfigureAwait(false);

        if (tokens != null && !string.IsNullOrEmpty(tokens.AccessToken))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && tokens?.RefreshToken != null)
        {
            await _authState.LogoutAsync().ConfigureAwait(false);
        }

        return response;
    }
}
