using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Application.Consent;

public interface IConsentService
{
    IReadOnlyCollection<string> GetScopesRequiringUserConsent(Client client, IReadOnlyCollection<string> requestedScopes);
    bool NeedsConsent(Client client, UserConsent? stored, IReadOnlyCollection<string> requestedScopes, DateTime now);
    Task RecordConsentAsync(Guid userId, Client client, IReadOnlyCollection<string> scopes, TimeSpan lifetime, CancellationToken ct = default);
}