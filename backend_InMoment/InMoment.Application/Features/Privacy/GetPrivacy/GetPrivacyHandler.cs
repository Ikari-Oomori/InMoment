using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Privacy.Common;
using InMoment.Domain.Common;
using InMoment.Domain.Privacy;
using MediatR;

namespace InMoment.Application.Features.Privacy.GetPrivacy;

public sealed class GetPrivacyHandler : IRequestHandler<GetPrivacyQuery, PrivacySettingsDto>
{
    private readonly IPrivacySettingsRepository _privacy;
    private readonly ICurrentUser _current;
    private readonly IUnitOfWork _uow;

    public GetPrivacyHandler(
        IPrivacySettingsRepository privacy,
        ICurrentUser current,
        IUnitOfWork uow)
    {
        _privacy = privacy;
        _current = current;
        _uow = uow;
    }

    public async Task<PrivacySettingsDto> Handle(GetPrivacyQuery request, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        var settings = await _privacy.GetByUserIdAsync(_current.UserId, ct);
        if (settings is null)
        {
            settings = PrivacySettings.CreateDefault(_current.UserId);
            await _privacy.AddAsync(settings, ct);
            await _uow.SaveChangesAsync(ct);
        }

        return new PrivacySettingsDto(
            settings.AllowFriendRequestsFrom,
            settings.AllowGroupInvitesFrom,
            settings.DiscoverableByContacts,
            settings.DiscoverableBySearch);
    }
}