using MediatR;

namespace InMoment.Application.Features.Friends.RejectRequest;

public sealed record RejectFriendRequestCommand(Guid RequestId) : IRequest;