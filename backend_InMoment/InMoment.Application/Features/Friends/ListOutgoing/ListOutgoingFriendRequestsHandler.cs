using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Friends.Common;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Friends.ListOutgoing;

public sealed class ListOutgoingFriendRequestsHandler
    : IRequestHandler<ListOutgoingFriendRequestsQuery, IReadOnlyList<FriendRequestDto>>
{
    private readonly IFriendRequestRepository _requests;
    private readonly IUserRepository _users;
    private readonly ICurrentUser _current;

    public ListOutgoingFriendRequestsHandler(
        IFriendRequestRepository requests,
        IUserRepository users,
        ICurrentUser current)
    {
        _requests = requests;
        _users = users;
        _current = current;
    }

    public async Task<IReadOnlyList<FriendRequestDto>> Handle(ListOutgoingFriendRequestsQuery query, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        var requests = await _requests.GetOutgoingPendingAsync(_current.UserId, ct);
        var result = new List<FriendRequestDto>(requests.Count);

        foreach (var request in requests)
        {
            var toUser = await _users.GetByIdAsync(request.ToUserId, ct);
            if (toUser is null) continue;

            result.Add(new FriendRequestDto(
                request.Id,
                toUser.Id,
                toUser.UserName,
                toUser.FirstName,
                toUser.LastName,
                toUser.ProfilePhotoUrl,
                request.Status,
                request.CreatedAtUtc));
        }

        return result;
    }
}