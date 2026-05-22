using InMoment.Application.Features.Notifications.Devices;
using InMoment.Application.Features.Notifications.GetUnreadCount;
using InMoment.Application.Features.Notifications.List;
using InMoment.Application.Features.Notifications.MarkAllRead;
using InMoment.Application.Features.Notifications.MarkRead;
using InMoment.Application.Features.Notifications.Settings;
using InMoment.Domain.Notifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InMoment.API.Modules.Notifications;

[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 100;

    private readonly IMediator _mediator;

    public NotificationsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<NotificationsPageDto>> List(
        [FromQuery] int limit = DefaultLimit,
        [FromQuery] string? cursor = null,
        CancellationToken ct = default)
    {
        var safeLimit = NormalizeLimit(limit, DefaultLimit, MaxLimit);
        var safeCursor = string.IsNullOrWhiteSpace(cursor) ? null : cursor.Trim();

        var result = await _mediator.Send(
            new ListNotificationsQuery(safeLimit, safeCursor),
            ct);

        return Ok(result);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadNotificationsCountDto>> GetUnreadCount(CancellationToken ct)
        => Ok(await _mediator.Send(new GetUnreadNotificationsCountQuery(), ct));

    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken ct)
    {
        await _mediator.Send(new MarkNotificationReadCommand(notificationId), ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await _mediator.Send(new MarkAllNotificationsReadCommand(), ct);
        return NoContent();
    }

    [HttpGet("settings")]
    public async Task<ActionResult<NotificationSettingsDto>> GetSettings(CancellationToken ct)
        => Ok(await _mediator.Send(new GetNotificationSettingsQuery(), ct));

    public sealed record UpdateNotificationSettingsRequest(
        bool PushEnabled,
        bool PushGroupInvitations,
        bool PushReactions,
        bool PushComments,
        bool PushReplies,
        bool PushMentions,
        bool PushPosts,
        bool PushRetention,
        bool PushProductUpdates);

    [HttpPut("settings")]
    public async Task<ActionResult<NotificationSettingsDto>> UpdateSettings(
        [FromBody] UpdateNotificationSettingsRequest request,
        CancellationToken ct)
    {
        var result = await _mediator.Send(
            new UpdateNotificationSettingsCommand(
                request.PushEnabled,
                request.PushGroupInvitations,
                request.PushReactions,
                request.PushComments,
                request.PushReplies,
                request.PushMentions,
                request.PushPosts,
                request.PushRetention,
                request.PushProductUpdates),
            ct);

        return Ok(result);
    }

    [HttpGet("devices")]
    public async Task<ActionResult<IReadOnlyList<DeviceTokenDto>>> ListDevices(CancellationToken ct)
        => Ok(await _mediator.Send(new ListMyDeviceTokensQuery(), ct));

    public sealed record RegisterDeviceRequest(
        string Token,
        PushPlatform Platform,
        PushProvider Provider,
        string? DeviceName);

    [HttpPost("devices")]
    public async Task<ActionResult<DeviceTokenDto>> RegisterDevice(
        [FromBody] RegisterDeviceRequest request,
        CancellationToken ct)
    {
        var result = await _mediator.Send(
            new RegisterDeviceTokenCommand(
                request.Token,
                request.Platform,
                request.Provider,
                request.DeviceName),
            ct);

        return Ok(result);
    }

    [HttpDelete("devices/{deviceTokenId:guid}")]
    public async Task<IActionResult> RevokeDevice(Guid deviceTokenId, CancellationToken ct)
    {
        await _mediator.Send(new RevokeDeviceTokenCommand(deviceTokenId), ct);
        return NoContent();
    }

    private static int NormalizeLimit(int value, int defaultValue, int maxValue)
    {
        if (value <= 0)
            return defaultValue;

        return value > maxValue ? maxValue : value;
    }
}