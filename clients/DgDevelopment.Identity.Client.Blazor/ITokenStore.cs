using DgDevelopment.Identity.Client.Core;

namespace DgDevelopment.Identity.Client.Blazor;

public interface ITokenStore
{
    Task<TokenResponse?> GetTokensAsync();
    Task SaveTokensAsync(TokenResponse tokens);
    Task ClearTokensAsync();
}
