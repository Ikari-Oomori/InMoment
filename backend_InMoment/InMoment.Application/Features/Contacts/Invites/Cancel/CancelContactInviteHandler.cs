using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Contacts.Invites.Cancel;

public sealed class CancelContactInviteHandler : IRequestHandler<CancelContactInviteCommand>
{
    private readonly IContactInviteRepository _invites;
    private readonly ICurrentUser _current;
    private readonly IUnitOfWork _uow;

    public CancelContactInviteHandler(
        IContactInviteRepository invites,
        ICurrentUser current,
        IUnitOfWork uow)
    {
        _invites = invites;
        _current = current;
        _uow = uow;
    }

    public async Task Handle(CancelContactInviteCommand request, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        if (request.InviteId == Guid.Empty)
            throw new ValidationException("InviteId is required.");

        var invite = await _invites.GetByIdAsync(request.InviteId, ct)
                     ?? throw new NotFoundException("Приглашение не найдено.");

        if (invite.UserId != _current.UserId)
            throw new ForbiddenException("Нельзя отменить чужое приглашение.");

        invite.Cancel();
        await _uow.SaveChangesAsync(ct);
    }
}