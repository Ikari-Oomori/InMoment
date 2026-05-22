using MediatR;

namespace InMoment.Application.Features.Groups.MakeAdmin;

public sealed record MakeAdminCommand(Guid GroupId, Guid UserId) : IRequest<Unit>;