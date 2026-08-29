namespace DgDevelopment.Identity.Server.UnitTests.Testing;

using DgDevelopment.Identity.Domain.Services;

public sealed class FakeSessionEventPublisher : ISessionEventPublisher
{
    public List<Guid> PublishedForUserIds { get; } = [];

    public Task PublishForceLogoutAsync(Guid userId, CancellationToken ct = default)
    {
        PublishedForUserIds.Add(userId);
        return Task.CompletedTask;
    }
}
