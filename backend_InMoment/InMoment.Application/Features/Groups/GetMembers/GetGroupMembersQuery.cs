using MediatR;

namespace InMoment.Application.Features.Groups.GetMembers;

public sealed record GetGroupMembersQuery(Guid GroupId) : IRequest<IReadOnlyList<GroupMemberDto>>;