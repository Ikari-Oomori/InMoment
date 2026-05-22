using InMoment.Domain.Notifications;

namespace InMoment.Application.Features.Notifications.Devices;

public sealed record DeviceTokenDto(
    Guid Id,
    string Token,
    PushPlatform Platform,
    PushProvider Provider,
    string? DeviceName,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime LastUsedAtUtc
);