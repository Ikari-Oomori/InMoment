using MediatR;

namespace InMoment.Application.Features.Groups.InviteUser;

public sealed record InviteUserCommand(Guid GroupId, string Login) : IRequest<InviteUserResult>;