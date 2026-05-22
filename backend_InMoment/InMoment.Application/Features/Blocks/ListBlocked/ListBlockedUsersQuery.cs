using InMoment.Application.Features.Blocks.Common;
using MediatR;

namespace InMoment.Application.Features.Blocks.ListBlocked;

public sealed record ListBlockedUsersQuery : IRequest<IReadOnlyList<BlockedUserDto>>;