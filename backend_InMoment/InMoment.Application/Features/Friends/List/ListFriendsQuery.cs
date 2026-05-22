using InMoment.Application.Features.Friends.Common;
using MediatR;

namespace InMoment.Application.Features.Friends.List;

public sealed record ListFriendsQuery() : IRequest<IReadOnlyList<FriendDto>>;