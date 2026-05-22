using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Realtime;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Notifications.Common;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;
using InMoment.Domain.Notifications;
using InMoment.Domain.Privacy;
using MediatR;

namespace InMoment.Application.Features.Groups.InviteUser;

public sealed class InviteUserHandler : IRequestHandler<InviteUserCommand, InviteUserResult>
{
    private readonly IGroupRepository _groups;
    private readonly IUserRepository _users;
    private readonly IInvitationRepository _invitations;
    private readonly INotificationRepository _notifications;
    private readonly INotificationRealtime _notificationRealtime;
    private readonly INotificationPushDeliveryService _pushDelivery;
    private readonly IPrivacySettingsRepository _privacy;
    private readonly IBlockedUserRepository _blocks;
    private readonly IFriendshipRepository _friendships;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _current;

    public InviteUserHandler(
        IGroupRepository groups,
        IUserRepository users,
        IInvitationRepository invitations,
        INotificationRepository notifications,
        INotificationRealtime notificationRealtime,
        INotificationPushDeliveryService pushDelivery,
        IPrivacySettingsRepository privacy,
        IBlockedUserRepository blocks,
        IFriendshipRepository friendships,
        IUnitOfWork uow,
        ICurrentUser current)
    {
        _groups = groups;
        _users = users;
        _invitations = invitations;
        _notifications = notifications;
        _notificationRealtime = notificationRealtime;
        _pushDelivery = pushDelivery;
        _privacy = privacy;
        _blocks = blocks;
        _friendships = friendships;
        _uow = uow;
        _current = current;
    }

    public async Task<InviteUserResult> Handle(InviteUserCommand cmd, CancellationToken ct)
    {
        if (cmd.GroupId == Guid.Empty)
            throw new ValidationException("GroupId is required.");

        if (string.IsNullOrWhiteSpace(cmd.Login))
            throw new ValidationException("Login is required.");

        var group = await _groups.GetByIdAsync(cmd.GroupId, ct)
                   ?? throw new NotFoundException("Group not found.");

        group.EnsureManager(_current.UserId);

        var login = cmd.Login.Trim();

        var invitedUser = login.Contains('@')
            ? await _users.GetByEmailAsync(login.ToLowerInvariant(), ct)
            : await _users.GetByUserNameAsync(login, ct);

        if (invitedUser is null || !invitedUser.IsActive)
            throw new NotFoundException("User not found.");

        if (invitedUser.Id == _current.UserId)
            throw new ValidationException("You cannot invite yourself.");

        if (await _blocks.ExistsEitherDirectionAsync(_current.UserId, invitedUser.Id, ct))
            throw new ForbiddenException("Взаимодействие с этим пользователем недоступно.");

        if (await _groups.IsMemberAsync(group.Id, invitedUser.Id, ct))
            throw new ValidationException("User is already a member of this group.");

        var privacy = await _privacy.GetByUserIdAsync(invitedUser.Id, ct);
        var friendship = await _friendships.GetByUsersAsync(_current.UserId, invitedUser.Id, ct);

        if (!CanReceiveGroupInvite(privacy, inviterIsFriend: friendship is not null))
            throw new ForbiddenException("Пользователь не принимает приглашения в группы.");

        if (await _invitations.HasPendingAsync(group.Id, invitedUser.Id, ct))
            throw new ValidationException("Pending invitation already exists.");

        var invitation = GroupInvitation.Create(group.Id, invitedUser.Id, _current.UserId);
        await _invitations.AddAsync(invitation, ct);

        Notification notificationForPush;

        var collapsed = await _notifications.FindLatestUnreadCollapsibleAsync(
            userId: invitedUser.Id,
            type: NotificationType.GroupInvitationReceived,
            actorUserId: _current.UserId,
            groupId: group.Id,
            photoId: null,
            ct: ct);

        if (collapsed is not null)
        {
            collapsed.CollapseWithLatestOccurrence(latestInvitationId: invitation.Id);
            notificationForPush = collapsed;
        }
        else
        {
            var notification = Notification.CreateGroupInvitationReceived(
                userId: invitedUser.Id,
                actorUserId: _current.UserId,
                groupId: group.Id,
                invitationId: invitation.Id);

            await _notifications.AddAsync(notification, ct);
            notificationForPush = notification;
        }

        await _uow.SaveChangesAsync(ct);

        var unreadCount = await _notifications.GetUnreadCountAsync(invitedUser.Id, ct);
        await _notificationRealtime.NotifyNotificationsChangedAsync(invitedUser.Id, unreadCount, ct);
        await _pushDelivery.TrySendAsync(notificationForPush, ct);

        return new InviteUserResult(invitation.Id);
    }

    private static bool CanReceiveGroupInvite(PrivacySettings? settings, bool inviterIsFriend)
    {
        if (settings is null)
            return true;

        return settings.AllowGroupInvitesFrom switch
        {
            PrivacyAudience.Everyone => true,
            PrivacyAudience.FriendsOnly => inviterIsFriend,
            PrivacyAudience.Nobody => false,
            _ => false
        };
    }
}