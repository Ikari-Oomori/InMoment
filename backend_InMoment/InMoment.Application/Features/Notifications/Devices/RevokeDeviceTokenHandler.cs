using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Notifications.Devices;

public sealed class RevokeDeviceTokenHandler : IRequestHandler<RevokeDeviceTokenCommand>
{
    private readonly IDeviceTokenRepository _deviceTokens;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _current;

    public RevokeDeviceTokenHandler(
        IDeviceTokenRepository deviceTokens,
        IUnitOfWork uow,
        ICurrentUser current)
    {
        _deviceTokens = deviceTokens;
        _uow = uow;
        _current = current;
    }

    public async Task Handle(RevokeDeviceTokenCommand request, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Unauthorized.");

        if (request.DeviceTokenId == Guid.Empty)
            throw new ValidationException("DeviceTokenId is required.");

        var token = await _deviceTokens.GetByIdAsync(request.DeviceTokenId, ct)
                    ?? throw new NotFoundException("Device token not found.");

        if (token.UserId != _current.UserId)
            throw new ForbiddenException("Forbidden.");

        token.Deactivate();

        await _uow.SaveChangesAsync(ct);
    }
}