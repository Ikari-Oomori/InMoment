using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Friends.Common;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Friends.ListIncoming;

public sealed class ListIncomingFriendRequestsHandler
    : IRequestHandler<ListIncomingFriendRequestsQuery, IReadOnlyList<FriendRequestDto>>
{
    private readonly IFriendRequestRepository _requests;
    private readonly IUserRepository _users;
    private readonly ICurrentUser _current;

    public ListIncomingFriendRequestsHandler(
        IFriendRequestRepository requests,
        IUserRepository users,
        ICurrentUser current)
    {
        _requests = requests;
        _users = users;
        _current = current;
    }

    public async Task<IReadOnlyList<FriendRequestDto>> Handle(ListIncomingFriendRequestsQuery query, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        var requests = await _requests.GetIncomingPendingAsync(_current.UserId, ct);
        var result = new List<FriendRequestDto>(requests.Count);

        foreach (var request in requests)
        {
            var fromUser = await _users.GetByIdAsync(request.FromUserId, ct);
            if (fromUser is null) continue;

            result.Add(new FriendRequestDto(
                request.Id,
                fromUser.Id,
                fromUser.UserName,
                fromUser.FirstName,
                fromUser.LastName,
                fromUser.ProfilePhotoUrl,
                request.Status,
                request.CreatedAtUtc));
        }

        return result;
    }
}