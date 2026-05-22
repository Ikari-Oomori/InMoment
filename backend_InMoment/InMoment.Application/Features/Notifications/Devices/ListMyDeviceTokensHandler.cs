using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Notifications.Devices;

public sealed class ListMyDeviceTokensHandler
    : IRequestHandler<ListMyDeviceTokensQuery, IReadOnlyList<DeviceTokenDto>>
{
    private readonly IDeviceTokenRepository _deviceTokens;
    private readonly ICurrentUser _current;

    public ListMyDeviceTokensHandler(
        IDeviceTokenRepository deviceTokens,
        ICurrentUser current)
    {
        _deviceTokens = deviceTokens;
        _current = current;
    }

    public async Task<IReadOnlyList<DeviceTokenDto>> Handle(ListMyDeviceTokensQuery request, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Unauthorized.");

        var tokens = await _deviceTokens.GetByUserIdAsync(_current.UserId, ct);

        return tokens
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.LastUsedAtUtc)
            .Select(x => new DeviceTokenDto(
                x.Id,
                x.Token,
                x.Platform,
                x.Provider,
                x.DeviceName,
                x.IsActive,
                x.CreatedAtUtc,
                x.UpdatedAtUtc,
                x.LastUsedAtUtc))
            .ToList();
    }
}