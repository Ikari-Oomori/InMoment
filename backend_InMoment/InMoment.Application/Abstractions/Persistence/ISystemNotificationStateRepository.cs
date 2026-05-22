using InMoment.Domain.Notifications;

namespace InMoment.Application.Abstractions.Persistence;

public interface ISystemNotificationStateRepository
{
    Task<SystemNotificationState?> GetByUserIdAsync(Guid userId, CancellationToken ct);
    Task AddAsync(SystemNotificationState state, CancellationToken ct);
}