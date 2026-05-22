using MediatR;

namespace InMoment.Application.Features.Notifications.Devices;

public sealed record ListMyDeviceTokensQuery() : IRequest<IReadOnlyList<DeviceTokenDto>>;