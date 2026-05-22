using FluentValidation;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using InMoment.Domain.Privacy;
using MediatR;

namespace InMoment.Application.Features.Privacy.UpdatePrivacy;

public sealed class UpdatePrivacyHandler : IRequestHandler<UpdatePrivacyCommand>
{
    private readonly IPrivacySettingsRepository _privacy;
    private readonly ICurrentUser _current;
    private readonly IUnitOfWork _uow;
    private readonly IValidator<UpdatePrivacyCommand> _validator;

    public UpdatePrivacyHandler(
        IPrivacySettingsRepository privacy,
        ICurrentUser current,
        IUnitOfWork uow,
        IValidator<UpdatePrivacyCommand> validator)
    {
        _privacy = privacy;
        _current = current;
        _uow = uow;
        _validator = validator;
    }

    public async Task Handle(UpdatePrivacyCommand cmd, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        await _validator.ValidateAndThrowAsync(cmd, ct);

        var settings = await _privacy.GetByUserIdAsync(_current.UserId, ct);
        if (settings is null)
        {
            settings = PrivacySettings.CreateDefault(_current.UserId);
            await _privacy.AddAsync(settings, ct);
        }

        settings.Update(
            cmd.AllowFriendRequestsFrom,
            cmd.AllowGroupInvitesFrom,
            cmd.DiscoverableByContacts,
            cmd.DiscoverableBySearch);

        await _uow.SaveChangesAsync(ct);
    }
}