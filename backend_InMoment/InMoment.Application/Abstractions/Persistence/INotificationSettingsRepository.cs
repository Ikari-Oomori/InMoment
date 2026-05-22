using InMoment.Domain.Notifications;

namespace InMoment.Application.Abstractions.Persistence;

public interface INotificationSettingsRepository
{
    Task<NotificationSettings?> GetByUserIdAsync(Guid userId, CancellationToken ct);
    Task AddAsync(NotificationSettings settings, CancellationToken ct);
}