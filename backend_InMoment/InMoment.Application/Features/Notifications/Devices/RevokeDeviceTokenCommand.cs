using MediatR;

namespace InMoment.Application.Features.Notifications.Devices;

public sealed record RevokeDeviceTokenCommand(Guid DeviceTokenId) : IRequest;