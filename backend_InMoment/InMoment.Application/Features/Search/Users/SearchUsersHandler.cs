using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Privacy;
using MediatR;

namespace InMoment.Application.Features.Search.Users;

public sealed class SearchUsersHandler : IRequestHandler<SearchUsersQuery, IReadOnlyList<SearchUserDto>>
{
    private readonly IUserRepository _users;
    private readonly IPrivacySettingsRepository _privacy;
    private readonly IBlockedUserRepository _blocks;
    private readonly ICurrentUser _current;

    public SearchUsersHandler(
        IUserRepository users,
        IPrivacySettingsRepository privacy,
        IBlockedUserRepository blocks,
        ICurrentUser current)
    {
        _users = users;
        _privacy = privacy;
        _blocks = blocks;
        _current = current;
    }

    public async Task<IReadOnlyList<SearchUserDto>> Handle(SearchUsersQuery q, CancellationToken ct)
    {
        var query = (q.Query ?? string.Empty).Trim();
        if (query.Length == 0)
            return Array.Empty<SearchUserDto>();

        var limit = q.Limit is < 1 or > 20 ? 10 : q.Limit;

        var items = await _users.SearchAsync(query, limit, _current.UserId, ct);
        var result = new List<SearchUserDto>(items.Count);

        foreach (var user in items)
        {
            if (!user.IsActive)
                continue;

            if (await _blocks.ExistsEitherDirectionAsync(_current.UserId, user.Id, ct))
                continue;

            var privacy = await _privacy.GetByUserIdAsync(user.Id, ct);
            if (!IsDiscoverableBySearch(privacy))
                continue;

            var displayName = BuildDisplayName(user.UserName, user.FirstName, user.LastName);

            result.Add(new SearchUserDto(
                user.Id,
                user.UserName,
                displayName,
                user.ProfilePhotoUrl));
        }

        return result;
    }

    private static bool IsDiscoverableBySearch(PrivacySettings? settings)
    {
        if (settings is null)
            return true;

        return settings.DiscoverableBySearch;
    }

    private static string BuildDisplayName(string userName, string firstName, string lastName)
    {
        var fullName = $"{firstName} {lastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? userName : fullName;
    }
}