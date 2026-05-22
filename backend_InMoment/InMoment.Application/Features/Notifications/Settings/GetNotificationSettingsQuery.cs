using MediatR;

namespace InMoment.Application.Features.Notifications.Settings;

public sealed record GetNotificationSettingsQuery() : IRequest<NotificationSettingsDto>;

public sealed record NotificationSettingsDto(
    bool PushEnabled,
    bool PushGroupInvitations,
    bool PushReactions,
    bool PushComments,
    bool PushReplies,
    bool PushMentions,
    bool PushPosts,
    bool PushRetention,
    bool PushProductUpdates,
    DateTime? CreatedAtUtc,
    DateTime? UpdatedAtUtc
);