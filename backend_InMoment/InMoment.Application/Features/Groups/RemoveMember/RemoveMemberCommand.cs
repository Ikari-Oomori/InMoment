using MediatR;

namespace InMoment.Application.Features.Groups.RemoveMember;

public sealed record RemoveMemberCommand(Guid GroupId, Guid UserId) : IRequest<Unit>;