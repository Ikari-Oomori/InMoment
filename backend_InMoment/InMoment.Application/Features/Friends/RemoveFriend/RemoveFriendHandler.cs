using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Friends.RemoveFriend;

public sealed class RemoveFriendHandler : IRequestHandler<RemoveFriendCommand>
{
    private readonly IFriendshipRepository _friendships;
    private readonly ICurrentUser _current;
    private readonly IUnitOfWork _uow;

    public RemoveFriendHandler(
        IFriendshipRepository friendships,
        ICurrentUser current,
        IUnitOfWork uow)
    {
        _friendships = friendships;
        _current = current;
        _uow = uow;
    }

    public async Task Handle(RemoveFriendCommand cmd, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        var friendship = await _friendships.GetByUsersAsync(_current.UserId, cmd.FriendUserId, ct)
                         ?? throw new NotFoundException("Дружба не найдена.");

        _friendships.Remove(friendship);
        await _uow.SaveChangesAsync(ct);
    }
}