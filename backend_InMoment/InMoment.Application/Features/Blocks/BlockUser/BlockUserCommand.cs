using MediatR;

namespace InMoment.Application.Features.Blocks.BlockUser;

public sealed record BlockUserCommand(Guid BlockedUserId) : IRequest;