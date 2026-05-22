using InMoment.Application.Features.Friends.Common;
using MediatR;

namespace InMoment.Application.Features.Friends.ListOutgoing;

public sealed record ListOutgoingFriendRequestsQuery() : IRequest<IReadOnlyList<FriendRequestDto>>;