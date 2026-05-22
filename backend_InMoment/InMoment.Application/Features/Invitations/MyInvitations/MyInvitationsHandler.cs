using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using MediatR;

namespace InMoment.Application.Features.Invitations.MyInvitations;

public sealed class MyInvitationsHandler
    : IRequestHandler<MyInvitationsQuery, IReadOnlyList<InvitationDto>>
{
    private readonly IInvitationRepository _invitations;
    private readonly ICurrentUser _current;

    public MyInvitationsHandler(IInvitationRepository invitations, ICurrentUser current)
    {
        _invitations = invitations;
        _current = current;
    }

    public async Task<IReadOnlyList<InvitationDto>> Handle(MyInvitationsQuery _, CancellationToken ct)
    {
        var list = await _invitations.GetPendingByInvitedUserIdAsync(_current.UserId, ct);

        return list
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new InvitationDto(x.Id, x.GroupId, x.InvitedByUserId, x.CreatedAt))
            .ToList();
    }
}