using InMoment.Domain.Security;

namespace InMoment.Application.Abstractions.Persistence;

public interface IRefreshSessionRepository
{
    Task AddAsync(RefreshSession session, CancellationToken ct);
    Task<RefreshSession?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<RefreshSession?> GetByTokenHashAsync(string tokenHash, CancellationToken ct);
    Task<IReadOnlyList<RefreshSession>> GetByUserIdAsync(Guid userId, CancellationToken ct);
}