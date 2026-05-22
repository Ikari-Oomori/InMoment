using InMoment.Domain.Notifications;

namespace InMoment.Application.Abstractions.Persistence;

public interface IDeviceTokenRepository
{
    Task AddAsync(DeviceToken token, CancellationToken ct);
    Task<DeviceToken?> GetByIdAsync(Guid deviceTokenId, CancellationToken ct);
    Task<DeviceToken?> GetByTokenAsync(string token, CancellationToken ct);
    Task<IReadOnlyList<DeviceToken>> GetByUserIdAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<DeviceToken>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct);
}