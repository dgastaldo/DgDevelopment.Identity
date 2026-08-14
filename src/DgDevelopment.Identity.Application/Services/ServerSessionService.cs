using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;

namespace DgDevelopment.Identity.Application.Services;

public sealed class ServerSessionService : IServerSessionService
{
    private static readonly TimeSpan StandardLifetime = TimeSpan.FromHours(8);
    private static readonly TimeSpan RememberMeLifetime = TimeSpan.FromDays(14);

    private readonly IUserSessionRepository _sessionRepository;

    public ServerSessionService(IUserSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository;
    }

    public Task<UserSession?> FindActiveAsync(Guid userId, CancellationToken ct = default)
    {
        return _sessionRepository.GetActiveByUserIdAsync(userId, ct);
    }

    public async Task<UserSession> CreateAsync(User user, bool rememberMe, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var expiresAt = rememberMe ? DateTime.UtcNow.Add(RememberMeLifetime) : DateTime.UtcNow.Add(StandardLifetime);
        var sessionId = Guid.NewGuid().ToString("N");
        var session = new UserSession(user.Id, sessionId, expiresAt, ["pwd"]);
        await _sessionRepository.AddAsync(session, ct).ConfigureAwait(false);
        return session;
    }
}