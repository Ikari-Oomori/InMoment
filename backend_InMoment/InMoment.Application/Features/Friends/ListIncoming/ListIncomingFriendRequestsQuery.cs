using InMoment.Application.Features.Friends.Common;
using MediatR;

namespace InMoment.Application.Features.Friends.ListIncoming;

public sealed record ListIncomingFriendRequestsQuery() : IRequest<IReadOnlyList<FriendRequestDto>>;