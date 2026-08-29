namespace DgDevelopment.Identity.Server.UnitTests.Testing;

using DgDevelopment.Identity.Domain.Services;

public sealed class FakeSessionEventPublisher : ISessionEventPublisher
{
    private readonly List<Guid> _publishedForUserIds = [];

    public IReadOnlyList<Guid> PublishedForUserIds => _publishedForUserIds;

    public Task PublishForceLogoutAsync(Guid userId, CancellationToken ct = default)
    {
        _publishedForUserIds.Add(userId);
        return Task.CompletedTask;
    }
}
