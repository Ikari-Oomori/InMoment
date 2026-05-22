using MediatR;

namespace InMoment.Application.Features.Friends.RemoveFriend;

public sealed record RemoveFriendCommand(Guid FriendUserId) : IRequest;