using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Groups.LeaveGroup;

public sealed class LeaveGroupHandler : IRequestHandler<LeaveGroupCommand, Unit>
{
    private readonly IGroupRepository _groups;
    private readonly IInvitationRepository _invitations;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _current;

    public LeaveGroupHandler(
        IGroupRepository groups,
        IInvitationRepository invitations,
        IUnitOfWork uow,
        ICurrentUser current)
    {
        _groups = groups;
        _invitations = invitations;
        _uow = uow;
        _current = current;
    }

    public async Task<Unit> Handle(LeaveGroupCommand cmd, CancellationToken ct)
    {
        var group = await _groups.GetByIdAsync(cmd.GroupId, ct)
                   ?? throw new NotFoundException("Group not found.");

        group.EnsureMember(_current.UserId);
        group.Leave(_current.UserId);

        await _invitations.CancelPendingByInviterAsync(group.Id, _current.UserId, ct);

        if (!group.IsActive)
            await _invitations.CancelPendingByGroupAsync(group.Id, ct);

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}