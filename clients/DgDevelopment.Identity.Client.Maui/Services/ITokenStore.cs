namespace DgDevelopment.Identity.Client.Maui.Services;

using System.Diagnostics.CodeAnalysis;
using DgDevelopment.Identity.Client.Core;

[SuppressMessage("Design", "CA1515", Justification = "Must stay public: it's a constructor parameter type on AuthSession, which itself must stay public.")]
public interface ITokenStore
{
    Task<TokenResponse?> GetTokensAsync();
    Task SaveTokensAsync(TokenResponse tokens);
    Task ClearTokensAsync();
}
