using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using InMoment.Domain.Notifications;
using MediatR;

namespace InMoment.Application.Features.Notifications.Settings;

public sealed class GetNotificationSettingsHandler
    : IRequestHandler<GetNotificationSettingsQuery, NotificationSettingsDto>
{
    private readonly INotificationSettingsRepository _settings;
    private readonly ICurrentUser _current;

    public GetNotificationSettingsHandler(
        INotificationSettingsRepository settings,
        ICurrentUser current)
    {
        _settings = settings;
        _current = current;
    }

    public async Task<NotificationSettingsDto> Handle(GetNotificationSettingsQuery request, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Unauthorized.");

        var settings = await _settings.GetByUserIdAsync(_current.UserId, ct)
                       ?? NotificationSettings.CreateDefault(_current.UserId);

        return new NotificationSettingsDto(
            settings.PushEnabled,
            settings.PushGroupInvitations,
            settings.PushReactions,
            settings.PushComments,
            settings.PushReplies,
            settings.PushMentions,
            settings.PushPosts,
            settings.PushRetention,
            settings.PushProductUpdates,
            settings.CreatedAtUtc,
            settings.UpdatedAtUtc);
    }
}