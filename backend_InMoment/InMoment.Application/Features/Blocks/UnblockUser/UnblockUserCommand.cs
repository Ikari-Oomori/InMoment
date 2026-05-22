using MediatR;

namespace InMoment.Application.Features.Blocks.UnblockUser;

public sealed record UnblockUserCommand(Guid BlockedUserId) : IRequest;