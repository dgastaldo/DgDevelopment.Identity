using System.Text.Json;
using System.Text.Json.Serialization;
using DgDevelopment.Identity.Client.Core;
using Microsoft.JSInterop;

namespace DgDevelopment.Identity.Client.Blazor;

public interface ISessionMarkerService
{
    Task SetAsync(UserInfo userInfo, CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
}

public sealed class SessionMarkerService(IJSRuntime jsRuntime) : ISessionMarkerService
{
    public const string CookieName = "identity_marker";
    private IJSObjectReference? _module;

    public async Task SetAsync(UserInfo userInfo, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(userInfo);
        if (string.IsNullOrEmpty(userInfo.Sub))
        {
            return;
        }

        var module = await GetModuleAsync(ct).ConfigureAwait(false);
        var payload = JsonSerializer.Serialize(new SessionMarkerDto(userInfo.Sub, userInfo.Name, userInfo.Email));
        await module.InvokeVoidAsync("setMarker", ct, CookieName, payload).ConfigureAwait(false);
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        var module = await GetModuleAsync(ct).ConfigureAwait(false);
        await module.InvokeVoidAsync("clearMarker", ct, CookieName).ConfigureAwait(false);
    }

    public static SessionMarkerDto? Decode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SessionMarkerDto>(raw);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async ValueTask<IJSObjectReference> GetModuleAsync(CancellationToken ct)
    {
        _module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", ct, "./_content/DgDevelopment.Identity.Client.Blazor/identitySession.js").ConfigureAwait(false);

        return _module;
    }
}

public record SessionMarkerDto(
    [property: JsonPropertyName("sub")] string Sub,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("email")] string? Email);