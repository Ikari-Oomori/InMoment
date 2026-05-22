using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using InMoment.Domain.Notifications;
using MediatR;

namespace InMoment.Application.Features.Notifications.Devices;

public sealed class RegisterDeviceTokenHandler
    : IRequestHandler<RegisterDeviceTokenCommand, DeviceTokenDto>
{
    private readonly IDeviceTokenRepository _deviceTokens;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _current;

    public RegisterDeviceTokenHandler(
        IDeviceTokenRepository deviceTokens,
        IUnitOfWork uow,
        ICurrentUser current)
    {
        _deviceTokens = deviceTokens;
        _uow = uow;
        _current = current;
    }

    public async Task<DeviceTokenDto> Handle(RegisterDeviceTokenCommand request, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Unauthorized.");

        if (string.IsNullOrWhiteSpace(request.Token))
            throw new ValidationException("Token is required.");

        var normalizedToken = request.Token.Trim();

        var existing = await _deviceTokens.GetByTokenAsync(normalizedToken, ct);

        if (existing is null)
        {
            existing = DeviceToken.Register(
                _current.UserId,
                normalizedToken,
                request.Platform,
                request.Provider,
                request.DeviceName);

            await _deviceTokens.AddAsync(existing, ct);
        }
        else if (existing.UserId == _current.UserId)
        {
            existing.Refresh(
                request.Platform,
                request.Provider,
                request.DeviceName);
        }
        else
        {
            existing.ReassignToUser(
                _current.UserId,
                request.Platform,
                request.Provider,
                request.DeviceName);
        }

        await _uow.SaveChangesAsync(ct);

        return new DeviceTokenDto(
            existing.Id,
            existing.Token,
            existing.Platform,
            existing.Provider,
            existing.DeviceName,
            existing.IsActive,
            existing.CreatedAtUtc,
            existing.UpdatedAtUtc,
            existing.LastUsedAtUtc);
    }
}