using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Features.Friends.Common;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Friends.List;

public sealed class ListFriendsHandler : IRequestHandler<ListFriendsQuery, IReadOnlyList<FriendDto>>
{
    private readonly IFriendshipRepository _friendships;
    private readonly IUserRepository _users;
    private readonly ICurrentUser _current;

    public ListFriendsHandler(
        IFriendshipRepository friendships,
        IUserRepository users,
        ICurrentUser current)
    {
        _friendships = friendships;
        _users = users;
        _current = current;
    }

    public async Task<IReadOnlyList<FriendDto>> Handle(ListFriendsQuery query, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        var friendships = await _friendships.GetByUserIdAsync(_current.UserId, ct);
        var result = new List<FriendDto>(friendships.Count);

        foreach (var friendship in friendships)
        {
            var otherUserId = friendship.GetOtherUserId(_current.UserId);
            var other = await _users.GetByIdAsync(otherUserId, ct);
            if (other is null) continue;

            result.Add(new FriendDto(
                other.Id,
                other.UserName,
                other.FirstName,
                other.LastName,
                other.ProfilePhotoUrl,
                friendship.CreatedAtUtc));
        }

        return result
            .OrderBy(x => x.FirstName)
            .ThenBy(x => x.LastName)
            .ToList();
    }
}