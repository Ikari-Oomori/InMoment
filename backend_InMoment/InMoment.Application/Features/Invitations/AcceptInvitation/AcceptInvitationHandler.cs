using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;
using MediatR;

namespace InMoment.Application.Features.Invitations.AcceptInvitation;

public sealed class AcceptInvitationHandler : IRequestHandler<AcceptInvitationCommand, Unit>
{
    private readonly IInvitationRepository _invitations;
    private readonly IGroupRepository _groups;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _current;

    public AcceptInvitationHandler(
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

    public async Task<Unit> Handle(AcceptInvitationCommand cmd, CancellationToken ct)
    {
        var inv = await _invitations.GetByIdAsync(cmd.InvitationId, ct)
                  ?? throw new NotFoundException("Invitation not found.");

        if (inv.InvitedUserId != _current.UserId)
            throw new ForbiddenException("You cannot accept this invitation.");

        if (inv.Status != InvitationStatus.Pending)
            throw new ValidationException("Only pending invitations can be accepted.");

        var inviterIsActive = await _invitations.InviterIsActiveMemberAsync(
            inv.GroupId, inv.InvitedByUserId, ct);

        if (!inviterIsActive)
            throw new ValidationException("Invitation is no longer valid.");

        var group = await _groups.GetByIdAsync(inv.GroupId, ct)
                   ?? throw new NotFoundException("Group not found.");

        if (group.IsMember(_current.UserId))
            throw new ValidationException("User is already a member of this group.");

        inv.Accept();

        var member = GroupMember.CreateMember(inv.GroupId, _current.UserId);
        await _groups.AddMemberAsync(member, ct);

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}