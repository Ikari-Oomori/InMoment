using MediatR;

namespace InMoment.Application.Features.Groups.TransferOwnership;

public sealed record TransferOwnershipCommand(Guid GroupId, Guid NewOwnerUserId)
    : IRequest<Unit>;