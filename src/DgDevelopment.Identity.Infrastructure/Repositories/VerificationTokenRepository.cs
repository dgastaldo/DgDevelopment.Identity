using Microsoft.EntityFrameworkCore;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Infrastructure.Data;

namespace DgDevelopment.Identity.Infrastructure.Repositories;

public sealed class VerificationTokenRepository(IdentityDbContext context) : IVerificationTokenRepository
{
    public Task<VerificationToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default)
        => context.VerificationTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task AddAsync(VerificationToken token, CancellationToken ct = default)
    {
        await context.VerificationTokens.AddAsync(token, ct).ConfigureAwait(false);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task MarkUsedAsync(Guid id, CancellationToken ct = default)
    {
        var token = await context.VerificationTokens.FindAsync([id], ct).ConfigureAwait(false);
        if (token is not null)
        {
            token.MarkUsed();
            await context.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
