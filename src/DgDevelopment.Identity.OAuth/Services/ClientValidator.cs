namespace DgDevelopment.Identity.OAuth.Services;

using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Domain.Repositories;

public sealed class ClientValidator(IClientRepository repository) : IClientValidator
{

    public async Task<ClientValidationResult> ValidateAsync(string? clientId, string? clientSecret, string grantType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return new(false, null, "Missing client_id.");

        var client = await repository.GetByClientIdAsync(clientId, ct).ConfigureAwait(false);
        if (client == null)
            return new(false, null, "Invalid client_id.");

        if (!client.IsActive)
            return new(false, null, "Client is deactivated.");

        if (!client.GrantTypes.Any(g => g.GrantType == grantType))
            return new(false, null, $"Grant type '{grantType}' not allowed for this client.");

        if (client.ClientType == Domain.Entities.ClientType.Confidential)
        {
            if (string.IsNullOrWhiteSpace(clientSecret))
                return new(false, null, "Missing client_secret.");

            var secretHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(clientSecret)));
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(secretHash),
                    Encoding.UTF8.GetBytes(client.ClientSecretHash)))
                return new(false, null, "Invalid client_secret.");
        }

        return new(true, client, null);
    }
}
