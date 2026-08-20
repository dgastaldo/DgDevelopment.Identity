using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Domain.Services;

public enum PushNotificationKind
{
    ChallengeNew,
    ChallengeResolved
}

public sealed record PushNotification(
    PushNotificationKind Kind,
    Guid UserId,
    string Title,
    string Body,
    string? ActionToken = null,
    string? ChallengeId = null);

public interface IPushNotifier
{
    Task SendAsync(PushNotification notification, IReadOnlyCollection<PushDevice> devices, CancellationToken ct = default);
}