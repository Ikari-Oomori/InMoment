using InMoment.Domain.Privacy;

namespace InMoment.Application.Abstractions.Persistence;

public interface IPrivacySettingsRepository
{
    Task AddAsync(PrivacySettings settings, CancellationToken ct);
    Task<PrivacySettings?> GetByUserIdAsync(Guid userId, CancellationToken ct);
}