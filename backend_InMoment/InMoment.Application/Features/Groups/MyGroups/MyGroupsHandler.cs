using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Groups;
using MediatR;

namespace InMoment.Application.Features.Groups.MyGroups;

public sealed class MyGroupsHandler : IRequestHandler<MyGroupsQuery, IReadOnlyList<MyGroupDto>>
{
    private readonly IGroupRepository _groups;
    private readonly ICurrentUser _currentUser;

    public MyGroupsHandler(
        IGroupRepository groups, 
        ICurrentUser currentUser)
    {
        _groups = groups;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<MyGroupDto>> Handle(MyGroupsQuery _, CancellationToken ct)
    {
        var list = await _groups.GetByUserIdAsync(_currentUser.UserId, ct);

        return list
            .Select(g =>
            {
                var myMembership = g.Members.FirstOrDefault(
                    m => m.IsActive && m.UserId == _currentUser.UserId);

                var isAdmin = myMembership?.Role is GroupRole.Owner or GroupRole.Admin;

                return new MyGroupDto(
                    g.Id,
                    g.Name,
                    g.Description,
                    g.AvatarUrl,
                    g.OwnerId,
                    isAdmin,
                    g.Members.Count(m => m.IsActive),
                    g.CreatedAt);
            })
            .ToList();
    }
}