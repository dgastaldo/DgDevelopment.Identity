namespace DgDevelopment.Identity.Server.Hubs;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

[Authorize(AuthenticationSchemes = "Identity.Partial")]
public sealed class MfaHub : Hub
{
    public Task SubscribeToUser(string userId)
    {
        if (!string.Equals(userId, Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, StringComparison.Ordinal))
            throw new HubException("You can only subscribe to your own channel.");

        return Groups.AddToGroupAsync(Context.ConnectionId, GroupForUser(userId));
    }

    public Task UnsubscribeFromUser(string userId)
    {
        if (!string.Equals(userId, Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, StringComparison.Ordinal))
            return Task.CompletedTask;

        return Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupForUser(userId));
    }

    public static string GroupForUser(string userId) => $"user:{userId}";

    public static string GroupForChallenge(string challengeId) => $"challenge:{challengeId}";
}
