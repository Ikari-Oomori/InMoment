using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Users.GetPublicProfile;

public sealed class GetPublicProfileHandler : IRequestHandler<GetPublicProfileQuery, PublicUserProfileDto>
{
    private readonly IUserRepository _users;
    private readonly IBlockedUserRepository _blockedUsers;
    private readonly ICurrentUser _current;

    public GetPublicProfileHandler(
        IUserRepository users,
        IBlockedUserRepository blockedUsers,
        ICurrentUser current)
    {
        _users = users;
        _blockedUsers = blockedUsers;
        _current = current;
    }

    public async Task<PublicUserProfileDto> Handle(GetPublicProfileQuery q, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Unauthorized.");

        var user = await _users.GetByIdAsync(q.UserId, ct)
                   ?? throw new NotFoundException("User not found.");

        if (!user.IsActive)
        {
            return new PublicUserProfileDto(
                Id: user.Id,
                UserName: string.Empty,
                FirstName: "Деактивированный",
                LastName: "пользователь",
                ProfilePhotoUrl: null,
                CreatedAt: user.CreatedAt,
                IsBlockedByMe: false,
                HasBlockedMe: false,
                IsActive: false,
                CanBlock: false,
                CanReport: false
            );
        }

        var isBlockedByMe = await _blockedUsers.ExistsAsync(_current.UserId, q.UserId, ct);
        var hasBlockedMe = await _blockedUsers.ExistsAsync(q.UserId, _current.UserId, ct);

        return new PublicUserProfileDto(
            Id: user.Id,
            UserName: user.UserName,
            FirstName: user.FirstName,
            LastName: user.LastName,
            ProfilePhotoUrl: user.ProfilePhotoUrl,
            CreatedAt: user.CreatedAt,
            IsBlockedByMe: isBlockedByMe,
            HasBlockedMe: hasBlockedMe,
            IsActive: true,
            CanBlock: true,
            CanReport: true
        );
    }
}