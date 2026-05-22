using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;

namespace InMoment.Application.Features.SystemAnnouncements.Update;

public sealed class UpdateSystemAnnouncementHandler
{
    private readonly ISystemModeratorAccess _moderatorAccess;
    private readonly ICurrentUser _currentUser;
    private readonly ISystemAnnouncementRepository _announcements;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateSystemAnnouncementHandler(
        ISystemModeratorAccess moderatorAccess,
        ICurrentUser currentUser,
        ISystemAnnouncementRepository announcements,
        IUnitOfWork unitOfWork)
    {
        _moderatorAccess = moderatorAccess;
        _currentUser = currentUser;
        _announcements = announcements;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(
        Guid id,
        string text,
        string? mediaUrl,
        string? mediaContentType,
        CancellationToken ct)
    {
        if (_currentUser.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        _moderatorAccess.EnsureModerator(_currentUser.UserId);

        var announcement = await _announcements.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Объявление не найдено.");

        announcement.Update(
            text,
            mediaUrl,
            mediaContentType,
            DateTime.UtcNow);

        await _unitOfWork.SaveChangesAsync(ct);
    }
}