using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Friends.RejectRequest;
using InMoment.Domain.Common;
using InMoment.Domain.Friends;

namespace InMoment.Application.Tests.Friends.RejectRequest;

public sealed class RejectFriendRequestHandlerTests
{
    private readonly Mock<IFriendRequestRepository> _requests = new();
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUserNotAuthorized()
    {
        // Arrange
        _current.SetupGet(x => x.UserId).Returns(Guid.Empty);

        var handler = CreateHandler();
        var command = new RejectFriendRequestCommand(Guid.NewGuid());

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
        var command = new RejectFriendRequestCommand(requestId);

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
        var command = new RejectFriendRequestCommand(request.Id);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Можно отклонить только входящую заявку.");
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenRequestIsNotPending()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var fromUserId = Guid.NewGuid();

        var request = FriendRequest.Create(fromUserId, currentUserId);
        request.Accept();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _requests.Setup(x => x.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var handler = CreateHandler();
        var command = new RejectFriendRequestCommand(request.Id);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Only a pending friend request can be changed.");
    }

    [Fact]
    public async Task Handle_ShouldRejectRequest_WhenValid()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var fromUserId = Guid.NewGuid();

        var request = FriendRequest.Create(fromUserId, currentUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _requests.Setup(x => x.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var handler = CreateHandler();
        var command = new RejectFriendRequestCommand(request.Id);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        request.Status.Should().Be(FriendRequestStatus.Rejected);
        request.RespondedAtUtc.Should().NotBeNull();

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
        var command = new RejectFriendRequestCommand(request.Id);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Можно отклонить только входящую заявку.");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private RejectFriendRequestHandler CreateHandler()
        => new(
            _requests.Object,
            _current.Object,
            _uow.Object);
}