using Microsoft.EntityFrameworkCore;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Infrastructure.Data;

namespace DgDevelopment.Identity.Infrastructure.Repositories;

public sealed class AuthorizationCodeRepository : IAuthorizationCodeRepository
{
    private readonly IdentityDbContext _context;

    public AuthorizationCodeRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<AuthorizationCode?> GetByCodeHashAsync(string codeHash, CancellationToken ct = default)
    {
        return await _context.AuthorizationCodes
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.CodeHash == codeHash, ct).ConfigureAwait(false);
    }

    public async Task AddAsync(AuthorizationCode code, CancellationToken ct = default)
    {
        await _context.AuthorizationCodes.AddAsync(code, ct).ConfigureAwait(false);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task MarkAsUsedAsync(Guid id, CancellationToken ct = default)
    {
        var code = await _context.AuthorizationCodes.FindAsync([id], ct).ConfigureAwait(false);
        if (code is not null)
        {
            code.MarkUsed();
            await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }

    public async Task DeleteExpiredAsync(CancellationToken ct = default)
    {
        await _context.AuthorizationCodes
            .Where(a => a.ExpiresAt < DateTime.UtcNow)
            .ExecuteDeleteAsync(ct).ConfigureAwait(false);
    }
}
