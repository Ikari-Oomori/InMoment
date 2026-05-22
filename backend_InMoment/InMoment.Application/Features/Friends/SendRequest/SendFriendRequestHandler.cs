using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using InMoment.Domain.Friends;
using InMoment.Domain.Privacy;
using MediatR;

namespace InMoment.Application.Features.Friends.SendRequest;

public sealed class SendFriendRequestHandler : IRequestHandler<SendFriendRequestCommand, Guid>
{
    private readonly IFriendRequestRepository _requests;
    private readonly IFriendshipRepository _friendships;
    private readonly IUserRepository _users;
    private readonly IPrivacySettingsRepository _privacy;
    private readonly IBlockedUserRepository _blocks;
    private readonly ICurrentUser _current;
    private readonly IUnitOfWork _uow;

    public SendFriendRequestHandler(
        IFriendRequestRepository requests,
        IFriendshipRepository friendships,
        IUserRepository users,
        IPrivacySettingsRepository privacy,
        IBlockedUserRepository blocks,
        ICurrentUser current,
        IUnitOfWork uow)
    {
        _requests = requests;
        _friendships = friendships;
        _users = users;
        _privacy = privacy;
        _blocks = blocks;
        _current = current;
        _uow = uow;
    }

    public async Task<Guid> Handle(SendFriendRequestCommand cmd, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        if (cmd.ToUserId == Guid.Empty)
            throw new ValidationException("ToUserId is required.");

        if (cmd.ToUserId == _current.UserId)
            throw new ValidationException("Нельзя отправить заявку самому себе.");

        var targetUser = await _users.GetByIdAsync(cmd.ToUserId, ct)
                         ?? throw new NotFoundException("Пользователь не найден.");

        if (!targetUser.IsActive)
            throw new NotFoundException("Пользователь не найден.");

        if (await _blocks.ExistsEitherDirectionAsync(_current.UserId, targetUser.Id, ct))
            throw new ForbiddenException("Взаимодействие с этим пользователем недоступно.");

        var existingFriendship = await _friendships.GetByUsersAsync(_current.UserId, targetUser.Id, ct);
        if (existingFriendship is not null)
            throw new ValidationException("Пользователи уже являются друзьями.");

        var settings = await _privacy.GetByUserIdAsync(targetUser.Id, ct);
        if (!CanReceiveFriendRequest(settings))
            throw new ForbiddenException("Пользователь не принимает заявки в друзья.");

        var existingPending = await _requests.GetPendingBetweenUsersAsync(_current.UserId, targetUser.Id, ct);
        if (existingPending is not null)
            throw new ValidationException("Между этими пользователями уже есть активная заявка.");

        var request = FriendRequest.Create(_current.UserId, targetUser.Id);

        await _requests.AddAsync(request, ct);
        await _uow.SaveChangesAsync(ct);

        return request.Id;
    }

    private static bool CanReceiveFriendRequest(PrivacySettings? settings)
    {
        if (settings is null)
            return true;

        return settings.AllowFriendRequestsFrom == PrivacyAudience.Everyone;
    }
}