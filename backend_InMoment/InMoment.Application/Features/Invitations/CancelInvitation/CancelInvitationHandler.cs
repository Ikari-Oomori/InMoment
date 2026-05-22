using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;
using MediatR;

namespace InMoment.Application.Features.Invitations.CancelInvitation;

public sealed class CancelInvitationHandler : IRequestHandler<CancelInvitationCommand, Unit>
{
    private readonly IInvitationRepository _invitations;
    private readonly IGroupRepository _groups;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _current;

    public CancelInvitationHandler(
        IInvitationRepository invitations,
        IGroupRepository groups,
        IUnitOfWork uow,
        ICurrentUser current)
    {
        _invitations = invitations;
        _groups = groups;
        _uow = uow;
        _current = current;
    }

    public async Task<Unit> Handle(CancelInvitationCommand cmd, CancellationToken ct)
    {
        var inv = await _invitations.GetByIdAsync(cmd.InvitationId, ct)
                  ?? throw new NotFoundException("Invitation not found.");

        if (inv.Status != InvitationStatus.Pending)
            throw new ValidationException("Only pending invitations can be cancelled.");

        if (inv.InvitedByUserId != _current.UserId)
        {
            var group = await _groups.GetByIdAsync(inv.GroupId, ct)
                       ?? throw new NotFoundException("Group not found.");

            group.EnsureOwner(_current.UserId);
        }

        inv.Cancel();
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}