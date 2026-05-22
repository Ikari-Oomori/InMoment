using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;

namespace InMoment.Application.Features.SystemAnnouncements.Delete;

public sealed class DeleteSystemAnnouncementHandler
{
    private readonly ISystemModeratorAccess _moderatorAccess;
    private readonly ICurrentUser _currentUser;
    private readonly ISystemAnnouncementRepository _announcements;
    private readonly INotificationRepository _notifications;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteSystemAnnouncementHandler(
        ISystemModeratorAccess moderatorAccess,
        ICurrentUser currentUser,
        ISystemAnnouncementRepository announcements,
        INotificationRepository notifications,
        IUnitOfWork unitOfWork)
    {
        _moderatorAccess = moderatorAccess;
        _currentUser = currentUser;
        _announcements = announcements;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        _moderatorAccess.EnsureModerator(_currentUser.UserId);

        var announcement = await _announcements.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Объявление не найдено.");

        var relatedNotifications =
            await _notifications.GetBySystemAnnouncementIdAsync(id, ct);

        _notifications.RemoveRange(relatedNotifications);
        _announcements.Remove(announcement);

        await _unitOfWork.SaveChangesAsync(ct);
    }
}