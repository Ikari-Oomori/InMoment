using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using InMoment.Domain.Privacy;
using MediatR;

namespace InMoment.Application.Features.Friends.Suggestions;

public sealed class SearchFriendSuggestionsHandler
    : IRequestHandler<SearchFriendSuggestionsQuery, IReadOnlyList<FriendSuggestionDto>>
{
    private readonly IUserRepository _users;
    private readonly IFriendshipRepository _friendships;
    private readonly IFriendRequestRepository _requests;
    private readonly IPrivacySettingsRepository _privacy;
    private readonly IBlockedUserRepository _blocks;
    private readonly ICurrentUser _current;

    public SearchFriendSuggestionsHandler(
        IUserRepository users,
        IFriendshipRepository friendships,
        IFriendRequestRepository requests,
        IPrivacySettingsRepository privacy,
        IBlockedUserRepository blocks,
        ICurrentUser current)
    {
        _users = users;
        _friendships = friendships;
        _requests = requests;
        _privacy = privacy;
        _blocks = blocks;
        _current = current;
    }

    public async Task<IReadOnlyList<FriendSuggestionDto>> Handle(
        SearchFriendSuggestionsQuery q,
        CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        var query = (q.Query ?? string.Empty).Trim();
        if (query.Length == 0)
            return Array.Empty<FriendSuggestionDto>();

        var limit = q.Limit is < 1 or > 20 ? 10 : q.Limit;

        var users = await _users.SearchAsync(query, limit * 3, _current.UserId, ct);
        if (users.Count == 0)
            return Array.Empty<FriendSuggestionDto>();

        var result = new List<FriendSuggestionDto>(limit);

        foreach (var user in users)
        {
            if (user.Id == _current.UserId)
                continue;

            if (await _blocks.ExistsEitherDirectionAsync(_current.UserId, user.Id, ct))
                continue;

            var settings = await _privacy.GetByUserIdAsync(user.Id, ct);
            if (!CanDiscover(settings))
                continue;

            var friendship = await _friendships.GetByUsersAsync(_current.UserId, user.Id, ct);
            var pending = await _requests.GetPendingBetweenUsersAsync(_current.UserId, user.Id, ct);

            var hasIncomingRequest = pending is not null && pending.ToUserId == _current.UserId;
            var hasOutgoingRequest = pending is not null && pending.FromUserId == _current.UserId;

            result.Add(new FriendSuggestionDto(
                UserId: user.Id,
                UserName: user.UserName,
                FirstName: user.FirstName,
                LastName: user.LastName,
                ProfilePhotoUrl: user.ProfilePhotoUrl,
                AlreadyFriend: friendship is not null,
                HasIncomingRequest: hasIncomingRequest,
                HasOutgoingRequest: hasOutgoingRequest
            ));

            if (result.Count == limit)
                break;
        }

        return result;
    }

    private static bool CanDiscover(PrivacySettings? settings)
    {
        if (settings is null)
            return true;

        return settings.DiscoverableBySearch;
    }
}