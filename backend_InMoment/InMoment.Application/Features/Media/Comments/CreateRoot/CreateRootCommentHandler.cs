using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Realtime;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Media.Comments.Common;
using InMoment.Application.Features.Notifications.Common;
using InMoment.Domain.Common;
using InMoment.Domain.Media;
using InMoment.Domain.Notifications;
using MediatR;

namespace InMoment.Application.Features.Media.Comments.CreateRoot;

public sealed class CreateRootCommentHandler : IRequestHandler<CreateRootCommentCommand, Guid>
{
    private readonly ICurrentUser _current;
    private readonly IPhotoRepository _photos;
    private readonly IGroupRepository _groups;
    private readonly ICommentRepository _comments;
    private readonly IUserRepository _users;
    private readonly INotificationRepository _notifications;
    private readonly INotificationRealtime _notificationRealtime;
    private readonly INotificationPushDeliveryService _pushDelivery;
    private readonly IBlockedUserRepository _blocks;
    private readonly IUnitOfWork _uow;
    private readonly IGroupRealtime _realtime;

    public CreateRootCommentHandler(
        ICurrentUser current,
        IPhotoRepository photos,
        IGroupRepository groups,
        ICommentRepository comments,
        IUserRepository users,
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
        _comments = comments;
        _users = users;
        _notifications = notifications;
        _notificationRealtime = notificationRealtime;
        _pushDelivery = pushDelivery;
        _blocks = blocks;
        _uow = uow;
        _realtime = realtime;
    }

    public async Task<Guid> Handle(CreateRootCommentCommand cmd, CancellationToken ct)
    {
        if (cmd.PhotoId == Guid.Empty)
            throw new ValidationException("PhotoId is required.");

        var photo = await _photos.GetByIdAsync(cmd.PhotoId, ct)
                   ?? throw new NotFoundException("Photo not found.");

        if (photo.IsDeleted)
            throw new NotFoundException("Photo not found.");

        var isMember = await _groups.IsMemberAsync(photo.GroupId, _current.UserId, ct);
        if (!isMember)
            throw new ForbiddenException("You are not an active member of this group.");

        if (await _blocks.ExistsEitherDirectionAsync(_current.UserId, photo.UploadedByUserId, ct))
            throw new ForbiddenException("Взаимодействие с этим пользователем недоступно.");

        var comment = Comment.CreateRoot(cmd.PhotoId, _current.UserId, cmd.Text, cmd.GifUrl);
        await _comments.AddAsync(comment, ct);

        var affectedNotificationUserIds = new HashSet<Guid>();
        var notificationsForPush = new List<Notification>();

        if (photo.UploadedByUserId != _current.UserId)
        {
            var collapsed = await _notifications.FindLatestUnreadCollapsibleAsync(
                userId: photo.UploadedByUserId,
                type: NotificationType.CommentOnPhoto,
                actorUserId: _current.UserId,
                groupId: photo.GroupId,
                photoId: photo.Id,
                ct: ct);

            if (collapsed is not null)
            {
                collapsed.CollapseWithLatestOccurrence(comment.Id);
                notificationsForPush.Add(collapsed);
            }
            else
            {
                var notification = Notification.CreateCommentOnPhoto(
                    userId: photo.UploadedByUserId,
                    actorUserId: _current.UserId,
                    groupId: photo.GroupId,
                    photoId: photo.Id,
                    commentId: comment.Id);

                await _notifications.AddAsync(notification, ct);
                notificationsForPush.Add(notification);
            }

            affectedNotificationUserIds.Add(photo.UploadedByUserId);
        }

        var mentionedUserNames = CommentMentionParser.ExtractUserNames(cmd.Text ?? string.Empty);

        foreach (var mentionedUserName in mentionedUserNames)
        {
            var mentionedUser = await _users.GetByUserNameAsync(mentionedUserName, ct);
            if (mentionedUser is null)
                continue;

            if (mentionedUser.Id == _current.UserId)
                continue;

            if (mentionedUser.Id == photo.UploadedByUserId)
                continue;

            if (await _blocks.ExistsEitherDirectionAsync(_current.UserId, mentionedUser.Id, ct))
                continue;

            var isMentionedUserMember = await _groups.IsMemberAsync(photo.GroupId, mentionedUser.Id, ct);
            if (!isMentionedUserMember)
                continue;

            var collapsed = await _notifications.FindLatestUnreadCollapsibleAsync(
                userId: mentionedUser.Id,
                type: NotificationType.CommentMention,
                actorUserId: _current.UserId,
                groupId: photo.GroupId,
                photoId: photo.Id,
                ct: ct);

            if (collapsed is not null)
            {
                collapsed.CollapseWithLatestOccurrence(comment.Id);
                notificationsForPush.Add(collapsed);
            }
            else
            {
                var notification = Notification.CreateCommentMention(
                    userId: mentionedUser.Id,
                    actorUserId: _current.UserId,
                    groupId: photo.GroupId,
                    photoId: photo.Id,
                    commentId: comment.Id);

                await _notifications.AddAsync(notification, ct);
                notificationsForPush.Add(notification);
            }

            affectedNotificationUserIds.Add(mentionedUser.Id);
        }

        await _uow.SaveChangesAsync(ct);

        await _realtime.NotifyFeedChangedAsync(photo.GroupId, "comment_changed", cmd.PhotoId, ct);

        foreach (var affectedUserId in affectedNotificationUserIds)
        {
            var unreadCount = await _notifications.GetUnreadCountAsync(affectedUserId, ct);
            await _notificationRealtime.NotifyNotificationsChangedAsync(affectedUserId, unreadCount, ct);
        }

        foreach (var notification in notificationsForPush)
            await _pushDelivery.TrySendAsync(notification, ct);

        return comment.Id;
    }
}