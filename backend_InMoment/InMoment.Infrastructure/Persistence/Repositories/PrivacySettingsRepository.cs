using InMoment.Application.Abstractions.Persistence;
using InMoment.Domain.Privacy;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Persistence.Repositories;

public sealed class PrivacySettingsRepository : IPrivacySettingsRepository
{
    private readonly AppDbContext _db;

    public PrivacySettingsRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(PrivacySettings settings, CancellationToken ct)
        => _db.Set<PrivacySettings>().AddAsync(settings, ct).AsTask();

    public Task<PrivacySettings?> GetByUserIdAsync(Guid userId, CancellationToken ct)
        => _db.Set<PrivacySettings>().FirstOrDefaultAsync(x => x.UserId == userId, ct);
}