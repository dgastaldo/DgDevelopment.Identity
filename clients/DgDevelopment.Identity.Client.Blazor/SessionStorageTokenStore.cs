using DgDevelopment.Identity.Client.Core;

namespace DgDevelopment.Identity.Client.Blazor;

public sealed class SessionStorageTokenStore : ITokenStore
{
    private readonly ISessionStorageService _storage;
    private const string Key = "identity_tokens";

    public SessionStorageTokenStore(ISessionStorageService storage)
    {
        _storage = storage;
    }

    public Task<TokenResponse?> GetTokensAsync() => _storage.GetItemAsync<TokenResponse>(Key);

    public Task SaveTokensAsync(TokenResponse tokens) => _storage.SetItemAsync(Key, tokens);

    public Task ClearTokensAsync() => _storage.RemoveItemAsync(Key);
}

public interface ISessionStorageService
{
    Task<T?> GetItemAsync<T>(string key);
    Task SetItemAsync<T>(string key, T value);
    Task RemoveItemAsync(string key);
}
