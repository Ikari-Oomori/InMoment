using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Blocks.Common;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Blocks.ListBlocked;

public sealed class ListBlockedUsersHandler
    : IRequestHandler<ListBlockedUsersQuery, IReadOnlyList<BlockedUserDto>>
{
    private readonly IBlockedUserRepository _blocks;
    private readonly IUserRepository _users;
    private readonly ICurrentUser _current;

    public ListBlockedUsersHandler(
        IBlockedUserRepository blocks,
        IUserRepository users,
        ICurrentUser current)
    {
        _blocks = blocks;
        _users = users;
        _current = current;
    }

    public async Task<IReadOnlyList<BlockedUserDto>> Handle(ListBlockedUsersQuery request, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        var rows = await _blocks.GetByUserIdAsync(_current.UserId, ct);
        var result = new List<BlockedUserDto>(rows.Count);

        foreach (var row in rows)
        {
            var user = await _users.GetByIdAsync(row.BlockedUserId, ct);
            if (user is null) continue;

            result.Add(new BlockedUserDto(
                user.Id,
                user.UserName,
                user.FirstName,
                user.LastName,
                user.ProfilePhotoUrl,
                row.CreatedAtUtc));
        }

        return result
            .OrderBy(x => x.FirstName)
            .ThenBy(x => x.LastName)
            .ToList();
    }
}