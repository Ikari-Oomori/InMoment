using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;
using MediatR;

namespace InMoment.Application.Features.Groups.GetMembers;

public sealed class GetGroupMembersHandler
    : IRequestHandler<GetGroupMembersQuery, IReadOnlyList<GroupMemberDto>>
{
    private readonly IGroupRepository _groups;
    private readonly IUserRepository _users;
    private readonly ICurrentUser _current;

    public GetGroupMembersHandler(
        IGroupRepository groups,
        IUserRepository users,
        ICurrentUser current)
    {
        _groups = groups;
        _users = users;
        _current = current;
    }

    public async Task<IReadOnlyList<GroupMemberDto>> Handle(
        GetGroupMembersQuery q,
        CancellationToken ct)
    {
        var group = await _groups.GetByIdAsync(q.GroupId, ct)
                   ?? throw new NotFoundException("Group not found.");

        if (!group.IsMember(_current.UserId))
            throw new ForbiddenException("You are not a member of this group.");

        var activeMembers = group.Members
            .Where(m => m.IsActive)
            .ToList();

        var memberIds = activeMembers
            .Select(m => m.UserId)
            .ToList();

        var users = await _users.GetByIdsAsync(memberIds, ct);

        var roleByUserId = activeMembers.ToDictionary(
            m => m.UserId,
            m => m.Role);

        return users
            .Select(u =>
            {
                var role = roleByUserId[u.Id];

                return new GroupMemberDto(
                    u.Id,
                    u.UserName,
                    u.FirstName,
                    u.LastName,
                    u.ProfilePhotoUrl,
                    role,
                    role == GroupRole.Owner,
                    role == GroupRole.Admin);
            })
            .OrderBy(x => x.Role)
            .ThenBy(x => x.UserName)
            .ToList();
    }
}