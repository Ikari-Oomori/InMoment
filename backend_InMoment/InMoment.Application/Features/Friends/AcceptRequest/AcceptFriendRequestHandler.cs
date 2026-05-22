using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using InMoment.Domain.Friends;
using MediatR;

namespace InMoment.Application.Features.Friends.AcceptRequest;

public sealed class AcceptFriendRequestHandler : IRequestHandler<AcceptFriendRequestCommand>
{
    private readonly IFriendRequestRepository _requests;
    private readonly IFriendshipRepository _friendships;
    private readonly ICurrentUser _current;
    private readonly IUnitOfWork _uow;

    public AcceptFriendRequestHandler(
        IFriendRequestRepository requests,
        IFriendshipRepository friendships,
        ICurrentUser current,
        IUnitOfWork uow)
    {
        _requests = requests;
        _friendships = friendships;
        _current = current;
        _uow = uow;
    }

    public async Task Handle(AcceptFriendRequestCommand cmd, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        var request = await _requests.GetByIdAsync(cmd.RequestId, ct)
                      ?? throw new NotFoundException("Заявка не найдена.");

        if (request.ToUserId != _current.UserId)
            throw new ForbiddenException("Можно принять только входящую заявку.");

        var existingFriendship = await _friendships.GetByUsersAsync(request.FromUserId, request.ToUserId, ct);
        if (existingFriendship is null)
        {
            var friendship = Friendship.Create(request.FromUserId, request.ToUserId);
            await _friendships.AddAsync(friendship, ct);
        }

        request.Accept();
        await _uow.SaveChangesAsync(ct);
    }
}