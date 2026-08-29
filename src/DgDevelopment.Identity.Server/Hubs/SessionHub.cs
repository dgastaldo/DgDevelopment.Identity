namespace DgDevelopment.Identity.Server.Hubs;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

/// <summary>
/// Pushes session-lifecycle events (right now: force-logout on password change) to a user's
/// connected clients in real time. Same group-per-user pattern as MfaHub, but on its own path
/// (/hubs/session) and scheme set: MfaHub is Identity.Partial-only because it's used mid-login,
/// before a full session exists, whereas this hub is for clients that are already fully signed
/// in - a browser tab (Cookie) or an external client like the MAUI app / IdentityPlatform WASM
/// (Bearer, since they never hold the server's own cookie).
/// </summary>
[Authorize(AuthenticationSchemes = "Cookies,Bearer")]
public sealed class SessionHub : Hub
{
    public Task SubscribeToUser(string userId)
    {
        if (!string.Equals(userId, GetCallerUserId(), StringComparison.Ordinal))
            throw new HubException("You can only subscribe to your own channel.");

        return Groups.AddToGroupAsync(Context.ConnectionId, GroupForUser(userId));
    }

    public Task UnsubscribeFromUser(string userId)
    {
        if (!string.Equals(userId, GetCallerUserId(), StringComparison.Ordinal))
            return Task.CompletedTask;

        return Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupForUser(userId));
    }

    public static string GroupForUser(string userId) => $"user:{userId}";

    // The Cookie scheme's claims use ClaimTypes.NameIdentifier (see e.g. Password.cshtml.cs's
    // SignInAsync), but the Bearer scheme has MapInboundClaims = false (Program.cs), which keeps
    // the JWT's raw "sub" claim instead of mapping it to ClaimTypes.NameIdentifier - the same
    // dual lookup MeController.GetUserId() already needs for the same reason.
    private string? GetCallerUserId()
        => Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Context.User?.FindFirst("sub")?.Value;
}
