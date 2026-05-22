using InMoment.Domain.Notifications;

namespace InMoment.Application.Abstractions.Communication;

public interface IPushSender
{
    Task SendAsync(PushSendRequest request, CancellationToken ct);
}

public sealed record PushSendTarget(
    Guid DeviceTokenId,
    string Token,
    PushPlatform Platform,
    PushProvider Provider);

public sealed record PushSendRequest(
    Guid UserId,
    NotificationType NotificationType,
    string Title,
    string Body,
    IReadOnlyList<PushSendTarget> Targets,
    IReadOnlyDictionary<string, string> Data);