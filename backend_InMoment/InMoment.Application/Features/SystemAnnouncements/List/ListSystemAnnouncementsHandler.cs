using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;

namespace InMoment.Application.Features.SystemAnnouncements.List;

public sealed class ListSystemAnnouncementsHandler
{
    private readonly ISystemModeratorAccess _moderatorAccess;
    private readonly ICurrentUser _currentUser;
    private readonly ISystemAnnouncementRepository _announcements;

    public ListSystemAnnouncementsHandler(
        ISystemModeratorAccess moderatorAccess,
        ICurrentUser currentUser,
        ISystemAnnouncementRepository announcements)
    {
        _moderatorAccess = moderatorAccess;
        _currentUser = currentUser;
        _announcements = announcements;
    }

    public async Task<IReadOnlyList<SystemAnnouncementDto>> Handle(
        int limit,
        CancellationToken ct)
    {
        if (_currentUser.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        _moderatorAccess.EnsureModerator(_currentUser.UserId);

        var safeLimit = limit is < 1 or > 100 ? 50 : limit;
        var now = DateTime.UtcNow;

        var items = await _announcements.GetLatestAsync(safeLimit, ct);

        return items
            .Select(x => new SystemAnnouncementDto(
                x.Id,
                x.Text,
                x.MediaUrl,
                x.MediaContentType,
                x.CreatedAtUtc,
                x.UpdatedAtUtc,
                x.CanEdit(now)))
            .ToList();
    }
}