using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using MediatR;

namespace InMoment.Application.Features.Search.Mentions;

public sealed class MentionUsersHandler : IRequestHandler<MentionUsersQuery, IReadOnlyList<MentionUserDto>>
{
    private readonly IUserRepository _users;
    private readonly IBlockedUserRepository _blocks;
    private readonly IGroupRepository _groups;
    private readonly ICurrentUser _current;

    public MentionUsersHandler(
        IUserRepository users,
        IBlockedUserRepository blocks,
        IGroupRepository groups,
        ICurrentUser current)
    {
        _users = users;
        _blocks = blocks;
        _groups = groups;
        _current = current;
    }

    public async Task<IReadOnlyList<MentionUserDto>> Handle(
        MentionUsersQuery q,
        CancellationToken ct)
    {
        var query = (q.Query ?? string.Empty).Trim();
        var limit = q.Limit is < 1 or > 10 ? 5 : q.Limit;

        HashSet<Guid>? allowedUserIds = null;

        if (q.GroupId.HasValue && q.GroupId.Value != Guid.Empty)
        {
            var isCurrentUserMember = await _groups.IsMemberAsync(
                q.GroupId.Value,
                _current.UserId,
                ct);

            if (!isCurrentUserMember)
                return Array.Empty<MentionUserDto>();

            var memberIds = await _groups.GetActiveMemberUserIdsAsync(q.GroupId.Value, ct);

            allowedUserIds = memberIds
                .Where(x => x != Guid.Empty && x != _current.UserId)
                .ToHashSet();

            if (allowedUserIds.Count == 0)
                return Array.Empty<MentionUserDto>();
        }

        IReadOnlyList<Domain.Users.User> users;

        if (string.IsNullOrWhiteSpace(query))
        {
            if (allowedUserIds is null)
                return Array.Empty<MentionUserDto>();

            var groupUsers = await _users.GetByIdsAsync(allowedUserIds.ToList(), ct);

            users = groupUsers
                .Where(x => x.IsActive)
                .OrderBy(x => x.UserName)
                .Take(limit * 3)
                .ToList();
        }
        else
        {
            users = await _users.SearchByPrefixAsync(
                query,
                limit * 3,
                _current.UserId,
                ct);
        }

        var result = new List<MentionUserDto>(limit);

        foreach (var u in users)
        {
            if (!u.IsActive)
                continue;

            if (allowedUserIds is not null && !allowedUserIds.Contains(u.Id))
                continue;

            if (await _blocks.ExistsEitherDirectionAsync(_current.UserId, u.Id, ct))
                continue;

            var displayName = BuildDisplayName(
                u.UserName,
                u.FirstName,
                u.LastName);

            result.Add(new MentionUserDto(
                u.Id,
                u.UserName,
                displayName,
                u.ProfilePhotoUrl));

            if (result.Count >= limit)
                break;
        }

        return result;
    }

    private static string BuildDisplayName(
        string userName,
        string firstName,
        string lastName)
    {
        var name = $"{firstName} {lastName}".Trim();

        if (string.IsNullOrWhiteSpace(name))
            return userName;

        return name;
    }
}