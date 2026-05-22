using MediatR;

namespace InMoment.Application.Features.Friends.SendRequest;

public sealed record SendFriendRequestCommand(Guid ToUserId) : IRequest<Guid>;