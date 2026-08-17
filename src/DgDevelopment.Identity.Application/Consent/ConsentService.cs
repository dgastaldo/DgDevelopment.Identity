using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;

namespace DgDevelopment.Identity.Application.Consent;

public sealed class ConsentService(IUserConsentRepository consentRepository) : IConsentService
{
    public IReadOnlyCollection<string> GetScopesRequiringUserConsent(Client client, IReadOnlyCollection<string> requestedScopes)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(requestedScopes);

        var adminApproved = client.AdminConsentScopes.Select(s => s.Scope).ToHashSet(StringComparer.Ordinal);
        return requestedScopes.Where(scope => !adminApproved.Contains(scope)).ToList();
    }

    public bool NeedsConsent(Client client, UserConsent? stored, IReadOnlyCollection<string> requestedScopes, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(requestedScopes);

        if (!client.RequireConsent)
            return false;

        var needUser = GetScopesRequiringUserConsent(client, requestedScopes);
        if (needUser.Count == 0)
            return false;

        if (stored is null || stored.IsExpired())
            return true;

        var granted = stored.GetScopes().ToHashSet(StringComparer.Ordinal);
        return needUser.Any(scope => !granted.Contains(scope));
    }

    public async Task RecordConsentAsync(Guid userId, Guid clientId, IReadOnlyCollection<string> scopes, TimeSpan lifetime, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        var existing = await consentRepository.GetAsync(userId, clientId, ct).ConfigureAwait(false);
        var merged = existing?.GetScopes().Concat(scopes).Distinct(StringComparer.Ordinal).ToArray()
            ?? scopes.Distinct(StringComparer.Ordinal).ToArray();

        var consent = new UserConsent(userId, clientId, merged, DateTime.UtcNow.Add(lifetime));
        await consentRepository.AddOrUpdateAsync(consent, ct).ConfigureAwait(false);
    }
}