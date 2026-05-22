using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using InMoment.Domain.Notifications;
using MediatR;

namespace InMoment.Application.Features.Notifications.Settings;

public sealed class UpdateNotificationSettingsHandler
    : IRequestHandler<UpdateNotificationSettingsCommand, NotificationSettingsDto>
{
    private readonly INotificationSettingsRepository _settings;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _current;

    public UpdateNotificationSettingsHandler(
        INotificationSettingsRepository settings,
        IUnitOfWork uow,
        ICurrentUser current)
    {
        _settings = settings;
        _uow = uow;
        _current = current;
    }

    public async Task<NotificationSettingsDto> Handle(UpdateNotificationSettingsCommand request, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Unauthorized.");

        var settings = await _settings.GetByUserIdAsync(_current.UserId, ct);

        if (settings is null)
        {
            settings = NotificationSettings.CreateDefault(_current.UserId);
            settings.Update(
                request.PushEnabled,
                request.PushGroupInvitations,
                request.PushReactions,
                request.PushComments,
                request.PushReplies,
                request.PushMentions,
                request.PushPosts,
                request.PushRetention,
                request.PushProductUpdates);

            await _settings.AddAsync(settings, ct);
        }
        else
        {
            settings.Update(
                request.PushEnabled,
                request.PushGroupInvitations,
                request.PushReactions,
                request.PushComments,
                request.PushReplies,
                request.PushMentions,
                request.PushPosts,
                request.PushRetention,
                request.PushProductUpdates);
        }

        await _uow.SaveChangesAsync(ct);

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