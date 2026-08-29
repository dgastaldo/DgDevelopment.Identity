namespace DgDevelopment.Identity.Server.Hubs;

using System.Globalization;
using DgDevelopment.Identity.Domain.Services;
using Microsoft.AspNetCore.SignalR;

public sealed class SignalRSessionEventPublisher(IHubContext<SessionHub> hubContext) : ISessionEventPublisher
{
    public Task PublishForceLogoutAsync(Guid userId, CancellationToken ct = default)
        => hubContext.Clients
            .Group(SessionHub.GroupForUser(userId.ToString(null, CultureInfo.InvariantCulture)))
            .SendAsync("ForceLogout", ct);
}
