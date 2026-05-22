using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;
using MediatR;

namespace InMoment.Application.Features.Invitations.RejectInvitation;

public sealed class RejectInvitationHandler : IRequestHandler<RejectInvitationCommand, Unit>
{
    private readonly IInvitationRepository _invitations;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _current;

    public RejectInvitationHandler(IInvitationRepository invitations, IUnitOfWork uow, ICurrentUser current)
    {
        _invitations = invitations;
        _uow = uow;
        _current = current;
    }

    public async Task<Unit> Handle(RejectInvitationCommand cmd, CancellationToken ct)
    {
        var inv = await _invitations.GetByIdAsync(cmd.InvitationId, ct)
                  ?? throw new NotFoundException("Invitation not found.");

        if (inv.InvitedUserId != _current.UserId)
            throw new ForbiddenException("You cannot reject this invitation.");

        if (inv.Status != InvitationStatus.Pending)
            throw new ValidationException("Only pending invitations can be rejected.");

        inv.Reject();
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}