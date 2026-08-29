namespace DgDevelopment.Identity.IntegrationTests;

using System.Collections.Concurrent;
using DgDevelopment.Identity.Domain.Services;

/// <summary>
/// Replaces the real <see cref="INotificationService"/> in tests: captures the body of the last
/// email sent to each address (keyed by recipient, so parallel tests using distinct addresses
/// don't clobber each other) instead of logging or sending, so tests can extract the
/// verification/reset link exactly as a real recipient would read it out of the email.
/// </summary>
public sealed class CapturingNotificationService : INotificationService
{
    private readonly ConcurrentDictionary<string, string> _lastBodySentTo = new(StringComparer.OrdinalIgnoreCase);

    public Task SendEmailAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        _lastBodySentTo[to] = body;
        return Task.CompletedTask;
    }

    public string? GetLastBodySentTo(string to) => _lastBodySentTo.GetValueOrDefault(to);
}
