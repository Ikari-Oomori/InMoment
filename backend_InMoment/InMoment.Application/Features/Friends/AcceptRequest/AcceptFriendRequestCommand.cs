using MediatR;

namespace InMoment.Application.Features.Friends.AcceptRequest;

public sealed record AcceptFriendRequestCommand(Guid RequestId) : IRequest;