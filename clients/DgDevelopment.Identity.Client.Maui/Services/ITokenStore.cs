namespace DgDevelopment.Identity.Client.Maui.Services;

using DgDevelopment.Identity.Client.Core;

public interface ITokenStore
{
    Task<TokenResponse?> GetTokensAsync();
    Task SaveTokensAsync(TokenResponse tokens);
    Task ClearTokensAsync();
}
