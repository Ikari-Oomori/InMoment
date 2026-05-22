using InMoment.Application.Abstractions.Persistence;
using InMoment.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Persistence.Repositories;

public sealed class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly AppDbContext _db;

    public PasswordResetTokenRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(PasswordResetToken token, CancellationToken ct)
        => _db.Set<PasswordResetToken>().AddAsync(token, ct).AsTask();

    public Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct)
        => _db.Set<PasswordResetToken>().FirstOrDefaultAsync(x => x.TokenHash == tokenHash, ct);

    public async Task<IReadOnlyList<PasswordResetToken>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        return await _db.Set<PasswordResetToken>()
            .Where(x =>
                x.UserId == userId &&
                x.UsedAtUtc == null &&
                x.RevokedAtUtc == null &&
                x.ExpiresAtUtc > now)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);
    }
}