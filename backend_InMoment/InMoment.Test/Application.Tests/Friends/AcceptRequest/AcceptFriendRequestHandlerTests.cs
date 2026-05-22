using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Friends.AcceptRequest;
using InMoment.Domain.Common;
using InMoment.Domain.Friends;

namespace InMoment.Application.Tests.Friends.AcceptRequest;

public sealed class AcceptFriendRequestHandlerTests
{
    private readonly Mock<IFriendRequestRepository> _requests = new();
    private readonly Mock<IFriendshipRepository> _friendships = new();
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUserNotAuthorized()
    {
        // Arrange
        _current.SetupGet(x => x.UserId).Returns(Guid.Empty);

        var handler = CreateHandler();
        var command = new AcceptFriendRequestCommand(Guid.NewGuid());

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Пользователь не авторизован.");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenRequestNotFound()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _requests.Setup(x => x.GetByIdAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FriendRequest?)null);

        var handler = CreateHandler();
        var command = new AcceptFriendRequestCommand(requestId);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Заявка не найдена.");
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenCurrentUserIsNotRecipient()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var fromUserId = Guid.NewGuid();
        var toUserId = Guid.NewGuid();

        var request = FriendRequest.Create(fromUserId, toUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _requests.Setup(x => x.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var handler = CreateHandler();
        var command = new AcceptFriendRequestCommand(request.Id);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Можно принять только входящую заявку.");
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenRequestIsNotPending()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var fromUserId = Guid.NewGuid();

        var request = FriendRequest.Create(fromUserId, currentUserId);
        request.Reject();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _requests.Setup(x => x.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var handler = CreateHandler();
        var command = new AcceptFriendRequestCommand(request.Id);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Only a pending friend request can be changed.");
    }

    [Fact]
    public async Task Handle_ShouldCreateFriendshipAndAcceptRequest_WhenNoFriendshipExists()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var fromUserId = Guid.NewGuid();

        var request = FriendRequest.Create(fromUserId, currentUserId);
        Friendship? createdFriendship = null;

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _requests.Setup(x => x.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        _friendships.Setup(x => x.GetByUsersAsync(fromUserId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Friendship?)null);

        _friendships.Setup(x => x.AddAsync(It.IsAny<Friendship>(), It.IsAny<CancellationToken>()))
            .Callback<Friendship, CancellationToken>((friendship, _) => createdFriendship = friendship)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var command = new AcceptFriendRequestCommand(request.Id);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        createdFriendship.Should().NotBeNull();
        createdFriendship!.Involves(fromUserId).Should().BeTrue();
        createdFriendship.Involves(currentUserId).Should().BeTrue();

        request.Status.Should().Be(FriendRequestStatus.Accepted);
        request.RespondedAtUtc.Should().NotBeNull();

        _friendships.Verify(x => x.AddAsync(It.IsAny<Friendship>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldAcceptRequestWithoutCreatingFriendship_WhenFriendshipAlreadyExists()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var fromUserId = Guid.NewGuid();

        var request = FriendRequest.Create(fromUserId, currentUserId);
        var existingFriendship = Friendship.Create(fromUserId, currentUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _requests.Setup(x => x.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        _friendships.Setup(x => x.GetByUsersAsync(fromUserId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFriendship);

        var handler = CreateHandler();
        var command = new AcceptFriendRequestCommand(request.Id);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        request.Status.Should().Be(FriendRequestStatus.Accepted);
        request.RespondedAtUtc.Should().NotBeNull();

        _friendships.Verify(x => x.AddAsync(It.IsAny<Friendship>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotSaveChanges_WhenValidationFails()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var request = FriendRequest.Create(Guid.NewGuid(), otherUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _requests.Setup(x => x.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var handler = CreateHandler();
        var command = new AcceptFriendRequestCommand(request.Id);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Можно принять только входящую заявку.");

        _friendships.Verify(x => x.AddAsync(It.IsAny<Friendship>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private AcceptFriendRequestHandler CreateHandler()
        => new(
            _requests.Object,
            _friendships.Object,
            _current.Object,
            _uow.Object);
}