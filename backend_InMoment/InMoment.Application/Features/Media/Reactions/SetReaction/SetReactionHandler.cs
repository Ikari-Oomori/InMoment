using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Realtime;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Notifications.Common;
using InMoment.Domain.Common;
using InMoment.Domain.Media;
using InMoment.Domain.Notifications;
using MediatR;

namespace InMoment.Application.Features.Media.Reactions.SetReaction;

public sealed class SetReactionHandler : IRequestHandler<SetReactionCommand>
{
    private readonly ICurrentUser _current;
    private readonly IPhotoRepository _photos;
    private readonly IGroupRepository _groups;
    private readonly IReactionRepository _reactions;
    private readonly INotificationRepository _notifications;
    private readonly INotificationRealtime _notificationRealtime;
    private readonly INotificationPushDeliveryService _pushDelivery;
    private readonly IBlockedUserRepository _blocks;
    private readonly IUnitOfWork _uow;
    private readonly IGroupRealtime _realtime;

    public SetReactionHandler(
        ICurrentUser current,
        IPhotoRepository photos,
        IGroupRepository groups,
        IReactionRepository reactions,
        INotificationRepository notifications,
        INotificationRealtime notificationRealtime,
        INotificationPushDeliveryService pushDelivery,
        IBlockedUserRepository blocks,
        IUnitOfWork uow,
        IGroupRealtime realtime)
    {
        _current = current;
        _photos = photos;
        _groups = groups;
        _reactions = reactions;
        _notifications = notifications;
        _notificationRealtime = notificationRealtime;
        _pushDelivery = pushDelivery;
        _blocks = blocks;
        _uow = uow;
        _realtime = realtime;
    }

    public async Task Handle(SetReactionCommand cmd, CancellationToken ct)
    {
        if (cmd.PhotoId == Guid.Empty)
            throw new ValidationException("PhotoId is required.");

        if (cmd.Type == ReactionType.None)
            throw new ValidationException("ReactionType is required.");

        var photo = await _photos.GetByIdAsync(cmd.PhotoId, ct)
                   ?? throw new NotFoundException("Photo not found.");

        if (photo.IsDeleted)
            throw new NotFoundException("Photo not found.");

        var isMember = await _groups.IsMemberAsync(photo.GroupId, _current.UserId, ct);
        if (!isMember)
            throw new ForbiddenException("You are not an active member of this group.");

        if (await _blocks.ExistsEitherDirectionAsync(_current.UserId, photo.UploadedByUserId, ct))
            throw new ForbiddenException("Взаимодействие с этим пользователем недоступно.");

        var existing = await _reactions.GetByPhotoAndUserAsync(cmd.PhotoId, _current.UserId, ct);

        var affectedNotificationUserId = Guid.Empty;
        Notification? notificationForPush = null;

        if (existing is null)
        {
            var reaction = Reaction.Create(cmd.PhotoId, _current.UserId, cmd.Type);
            await _reactions.AddAsync(reaction, ct);

            if (photo.UploadedByUserId != _current.UserId)
            {
                var collapsed = await _notifications.FindLatestUnreadCollapsibleAsync(
                    userId: photo.UploadedByUserId,
                    type: NotificationType.ReactionOnPhoto,
                    actorUserId: _current.UserId,
                    groupId: photo.GroupId,
                    photoId: photo.Id,
                    ct: ct);

                if (collapsed is not null)
                {
                    collapsed.CollapseWithLatestOccurrence();
                    notificationForPush = collapsed;
                }
                else
                {
                    var notification = Notification.CreateReactionOnPhoto(
                        userId: photo.UploadedByUserId,
                        actorUserId: _current.UserId,
                        groupId: photo.GroupId,
                        photoId: photo.Id);

                    await _notifications.AddAsync(notification, ct);
                    notificationForPush = notification;
                }

                affectedNotificationUserId = photo.UploadedByUserId;
            }
        }
        else
        {
            existing.Change(cmd.Type);
        }

        await _uow.SaveChangesAsync(ct);

        await _realtime.NotifyFeedChangedAsync(photo.GroupId, "reaction_changed", cmd.PhotoId, ct);

        if (affectedNotificationUserId != Guid.Empty)
        {
            var unreadCount = await _notifications.GetUnreadCountAsync(affectedNotificationUserId, ct);
            await _notificationRealtime.NotifyNotificationsChangedAsync(affectedNotificationUserId, unreadCount, ct);

            if (notificationForPush is not null)
                await _pushDelivery.TrySendAsync(notificationForPush, ct);
        }
    }
}