using System.Net.Http.Json;
using System.Text.Json;
using DgDevelopment.Identity.Client.Core;
using Microsoft.JSInterop;

namespace DgDevelopment.Identity.Client.Blazor;

public sealed class BrowserSessionStorage : ISessionStorageService
{
    private readonly IJSRuntime _jsRuntime;

    public BrowserSessionStorage(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<T?> GetItemAsync<T>(string key)
    {
        var json = await _jsRuntime.InvokeAsync<string>("sessionStorage.getItem", key).ConfigureAwait(false);
        return json == null ? default : JsonSerializer.Deserialize<T>(json);
    }

    public async Task SetItemAsync<T>(string key, T value)
    {
        var json = JsonSerializer.Serialize(value);
        await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", key, json).ConfigureAwait(false);
    }

    public async Task RemoveItemAsync(string key)
    {
        await _jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", key).ConfigureAwait(false);
    }
}
