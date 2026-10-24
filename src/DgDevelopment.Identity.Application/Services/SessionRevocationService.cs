using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Domain.Services;

namespace DgDevelopment.Identity.Application.Services;

public sealed class SessionRevocationService(
    IUserSessionRepository sessionRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ISessionEventPublisher eventPublisher) : ISessionRevocationService
{
    public async Task RevokeAllAsync(Guid userId, CancellationToken ct = default)
    {
        await sessionRepository.RevokeAllForUserAsync(userId, ct).ConfigureAwait(false);
        await refreshTokenRepository.RevokeAllForUserAsync(userId, ct).ConfigureAwait(false);
        await eventPublisher.PublishForceLogoutAsync(userId, ct).ConfigureAwait(false);
    }
}
