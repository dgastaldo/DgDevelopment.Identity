namespace DgDevelopment.Identity.Client.Core;

using System.Text;
using System.Text.Json;

/// <summary>
/// Reads claims out of a JWT payload without verifying its signature. Safe here because the token
/// arrives directly from /connect/token over a trusted channel - this is only used to drive UI
/// (nav visibility), never as an authorization boundary; every API call is still enforced server-side.
/// </summary>
public static class JwtClaimsReader
{
    public static IReadOnlyCollection<string> GetPermissions(string? jwt)
    {
        var claims = GetClaims(jwt);
        if (claims is null || !claims.TryGetValue("permission", out var value))
            return [];

        return value.ValueKind switch
        {
            JsonValueKind.Array => value.EnumerateArray().Select(e => e.GetString()).Where(s => s is not null).Select(s => s!).ToArray(),
            JsonValueKind.String => value.GetString() is { } single ? [single] : [],
            _ => []
        };
    }

    private static Dictionary<string, JsonElement>? GetClaims(string? jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt))
            return null;

        var parts = jwt.Split('.');
        if (parts.Length < 2)
            return null;

        try
        {
            var payload = Base64UrlDecode(parts[1]);
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payload);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        padded = (padded.Length % 4) switch
        {
            2 => padded + "==",
            3 => padded + "=",
            _ => padded
        };
        return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }
}
