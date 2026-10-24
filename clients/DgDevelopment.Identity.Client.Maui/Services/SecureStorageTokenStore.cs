namespace DgDevelopment.Identity.Client.Maui.Services;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using DgDevelopment.Identity.Client.Core;

[SuppressMessage("Performance", "CA1812", Justification = "Constructed via DI (AddSingleton<ITokenStore, SecureStorageTokenStore>() in MauiProgram.cs), not by direct instantiation.")]
internal sealed class SecureStorageTokenStore : ITokenStore
{
    private const string Key = "identity_tokens";

    public async Task<TokenResponse?> GetTokensAsync()
    {
        var json = await SecureStorage.Default.GetAsync(Key).ConfigureAwait(false);
        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<TokenResponse>(json);
    }

    public Task SaveTokensAsync(TokenResponse tokens)
        => SecureStorage.Default.SetAsync(Key, JsonSerializer.Serialize(tokens));

    public Task ClearTokensAsync()
    {
        SecureStorage.Default.Remove(Key);
        return Task.CompletedTask;
    }
}
