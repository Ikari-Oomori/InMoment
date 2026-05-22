using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Abstractions.Storage;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Users.GetWidget;

public sealed class GetWidgetHandler : IRequestHandler<GetWidgetQuery, WidgetDto>
{
    private readonly IUserRepository _users;
    private readonly IGroupRepository _groups;
    private readonly IPhotoRepository _photos;
    private readonly INotificationRepository _notifications;
    private readonly IFileStorage _storage;
    private readonly ICurrentUser _current;

    public GetWidgetHandler(
        IUserRepository users,
        IGroupRepository groups,
        IPhotoRepository photos,
        INotificationRepository notifications,
        IFileStorage storage,
        ICurrentUser current)
    {
        _users = users;
        _groups = groups;
        _photos = photos;
        _notifications = notifications;
        _storage = storage;
        _current = current;
    }

    public async Task<WidgetDto> Handle(GetWidgetQuery q, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(_current.UserId, ct)
                   ?? throw new NotFoundException("User not found.");

        if (!user.ActiveGroupId.HasValue)
            return new WidgetDto(null, null, null, null, null, null, 0);

        var group = await _groups.GetByIdAsync(user.ActiveGroupId.Value, ct);
        if (group is null || !group.IsMember(_current.UserId))
            return new WidgetDto(null, null, null, null, null, null, 0);

        var latest = await _photos.GetLatestByGroupAsync(group.Id, ct);

        var latestPhotoUrl = latest is null
            ? null
            : _storage.GetPublicUrl(latest.StorageKey);

        var newReactionsCount = latest is null
            ? 0
            : await _notifications.GetUnreadReactionCountForPhotoAsync(
                _current.UserId,
                group.Id,
                latest.Id,
                ct);

        return new WidgetDto(
            group.Id,
            group.Name,
            group.AvatarUrl,
            latest?.Id,
            latestPhotoUrl,
            latest?.CreatedAt,
            newReactionsCount
        );
    }
}