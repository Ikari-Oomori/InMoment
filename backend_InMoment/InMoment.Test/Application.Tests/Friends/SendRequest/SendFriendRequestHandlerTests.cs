using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Friends.SendRequest;
using InMoment.Domain.Common;
using InMoment.Domain.Friends;
using InMoment.Domain.Privacy;

namespace InMoment.Application.Tests.Friends.SendRequest;

public sealed class SendFriendRequestHandlerTests
{
    private readonly Mock<IFriendRequestRepository> _requests = new();
    private readonly Mock<IFriendshipRepository> _friendships = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPrivacySettingsRepository> _privacy = new();
    private readonly Mock<IBlockedUserRepository> _blocks = new();
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    [Fact]
    public async Task Handle_ShouldThrowForbidden_WhenUserNotAuthorized()
    {
        _current.SetupGet(x => x.UserId).Returns(Guid.Empty);

        var handler = CreateHandler();
        var command = new SendFriendRequestCommand(Guid.NewGuid());

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Пользователь не авторизован.");
    }

    [Fact]
    public async Task Handle_ShouldThrowValidation_WhenToUserIdEmpty()
    {
        _current.SetupGet(x => x.UserId).Returns(Guid.NewGuid());

        var handler = CreateHandler();
        var command = new SendFriendRequestCommand(Guid.Empty);

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("ToUserId is required.");
    }

    [Fact]
    public async Task Handle_ShouldThrowValidation_WhenSendingToSelf()
    {
        var userId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(userId);

        var handler = CreateHandler();
        var command = new SendFriendRequestCommand(userId);

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Нельзя отправить заявку самому себе.");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenTargetUserNotFound()
    {
        var userId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(userId);

        _users.Setup(x => x.GetByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Users.User?)null);

        var handler = CreateHandler();
        var command = new SendFriendRequestCommand(targetId);

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Пользователь не найден.");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenTargetUserInactive()
    {
        var userId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var targetUser = CreateUser(targetId);
        targetUser.Deactivate();

        _current.SetupGet(x => x.UserId).Returns(userId);

        _users.Setup(x => x.GetByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);

        var handler = CreateHandler();
        var command = new SendFriendRequestCommand(targetId);

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Пользователь не найден.");
    }

    [Fact]
    public async Task Handle_ShouldThrowForbidden_WhenUsersAreBlocked()
    {
        var userId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var targetUser = CreateUser(targetId);

        _current.SetupGet(x => x.UserId).Returns(userId);

        _users.Setup(x => x.GetByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(userId, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler();
        var command = new SendFriendRequestCommand(targetId);

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Взаимодействие с этим пользователем недоступно.");
    }

    [Fact]
    public async Task Handle_ShouldThrowValidation_WhenAlreadyFriends()
    {
        var userId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var targetUser = CreateUser(targetId);
        var friendship = Friendship.Create(userId, targetId);

        _current.SetupGet(x => x.UserId).Returns(userId);

        _users.Setup(x => x.GetByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(userId, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _friendships.Setup(x => x.GetByUsersAsync(userId, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(friendship);

        var handler = CreateHandler();
        var command = new SendFriendRequestCommand(targetId);

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Пользователи уже являются друзьями.");
    }

    [Fact]
    public async Task Handle_ShouldThrowForbidden_WhenPrivacyDisallowsRequests()
    {
        var userId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var targetUser = CreateUser(targetId);

        var settings = PrivacySettings.CreateDefault(targetId);
        settings.Update(
            PrivacyAudience.Nobody,
            PrivacyAudience.Everyone,
            true,
            true);

        _current.SetupGet(x => x.UserId).Returns(userId);

        _users.Setup(x => x.GetByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(userId, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _friendships.Setup(x => x.GetByUsersAsync(userId, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Friendship?)null);

        _privacy.Setup(x => x.GetByUserIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var handler = CreateHandler();
        var command = new SendFriendRequestCommand(targetId);

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Пользователь не принимает заявки в друзья.");
    }

    [Fact]
    public async Task Handle_ShouldThrowValidation_WhenPendingAlreadyExists()
    {
        var userId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var targetUser = CreateUser(targetId);
        var existingRequest = FriendRequest.Create(userId, targetId);

        _current.SetupGet(x => x.UserId).Returns(userId);

        _users.Setup(x => x.GetByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(userId, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _friendships.Setup(x => x.GetByUsersAsync(userId, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Friendship?)null);

        _privacy.Setup(x => x.GetByUserIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivacySettings?)null);

        _requests.Setup(x => x.GetPendingBetweenUsersAsync(userId, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRequest);

        var handler = CreateHandler();
        var command = new SendFriendRequestCommand(targetId);

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Между этими пользователями уже есть активная заявка.");
    }

    [Fact]
    public async Task Handle_ShouldCreateRequest_WhenValid()
    {
        var userId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var targetUser = CreateUser(targetId);

        FriendRequest? created = null;

        _current.SetupGet(x => x.UserId).Returns(userId);

        _users.Setup(x => x.GetByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(userId, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _friendships.Setup(x => x.GetByUsersAsync(userId, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Friendship?)null);

        _privacy.Setup(x => x.GetByUserIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivacySettings?)null);

        _requests.Setup(x => x.GetPendingBetweenUsersAsync(userId, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FriendRequest?)null);

        _requests.Setup(x => x.AddAsync(It.IsAny<FriendRequest>(), It.IsAny<CancellationToken>()))
            .Callback<FriendRequest, CancellationToken>((r, _) => created = r)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var command = new SendFriendRequestCommand(targetId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBe(Guid.Empty);

        created.Should().NotBeNull();
        created!.FromUserId.Should().Be(userId);
        created.ToUserId.Should().Be(targetId);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private SendFriendRequestHandler CreateHandler()
        => new(
            _requests.Object,
            _friendships.Object,
            _users.Object,
            _privacy.Object,
            _blocks.Object,
            _current.Object,
            _uow.Object);

    private static Domain.Users.User CreateUser(Guid id)
    {
        var user = Domain.Users.User.Create(
            email: $"{Guid.NewGuid()}@test.com",
            passwordHash: "hash",
            userName: Guid.NewGuid().ToString(),
            firstName: "Test",
            lastName: "User");

        typeof(Domain.Users.User)
            .GetProperty(nameof(Domain.Users.User.Id))!
            .SetValue(user, id);

        return user;
    }
}