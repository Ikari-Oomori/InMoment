using InMoment.Domain.Notifications;
using MediatR;

namespace InMoment.Application.Features.Notifications.Devices;

public sealed record RegisterDeviceTokenCommand(
    string Token,
    PushPlatform Platform,
    PushProvider Provider,
    string? DeviceName) : IRequest<DeviceTokenDto>;