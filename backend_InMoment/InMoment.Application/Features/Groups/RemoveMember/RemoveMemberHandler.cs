using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Groups.RemoveMember;

public sealed class RemoveMemberHandler : IRequestHandler<RemoveMemberCommand, Unit>
{
    private readonly IGroupRepository _groups;
    private readonly IInvitationRepository _invitations;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _current;

    public RemoveMemberHandler(
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

    public async Task<Unit> Handle(RemoveMemberCommand cmd, CancellationToken ct)
    {
        var group = await _groups.GetByIdAsync(cmd.GroupId, ct)
                   ?? throw new NotFoundException("Group not found.");

        group.RemoveMember(_current.UserId, cmd.UserId);

        await _invitations.CancelPendingByInviterAsync(group.Id, cmd.UserId, ct);

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}