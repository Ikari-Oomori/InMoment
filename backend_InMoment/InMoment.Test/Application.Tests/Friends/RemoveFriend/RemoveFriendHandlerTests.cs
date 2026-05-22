using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Friends.RemoveFriend;
using InMoment.Domain.Common;
using InMoment.Domain.Friends;

namespace InMoment.Application.Tests.Friends.RemoveFriend;

public sealed class RemoveFriendHandlerTests
{
    private readonly Mock<IFriendshipRepository> _friendships = new();
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUserNotAuthorized()
    {
        // Arrange
        _current.SetupGet(x => x.UserId).Returns(Guid.Empty);

        var handler = CreateHandler();
        var command = new RemoveFriendCommand(Guid.NewGuid());

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Пользователь не авторизован.");

        _friendships.Verify(x => x.Remove(It.IsAny<Friendship>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenFriendshipNotFound()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var friendUserId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _friendships.Setup(x => x.GetByUsersAsync(currentUserId, friendUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Friendship?)null);

        var handler = CreateHandler();
        var command = new RemoveFriendCommand(friendUserId);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Дружба не найдена.");

        _friendships.Verify(x => x.Remove(It.IsAny<Friendship>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRemoveFriendship_WhenFriendshipExists()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var friendUserId = Guid.NewGuid();
        var friendship = Friendship.Create(currentUserId, friendUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _friendships.Setup(x => x.GetByUsersAsync(currentUserId, friendUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(friendship);

        Friendship? removedFriendship = null;
        _friendships.Setup(x => x.Remove(It.IsAny<Friendship>()))
            .Callback<Friendship>(f => removedFriendship = f);

        var handler = CreateHandler();
        var command = new RemoveFriendCommand(friendUserId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        removedFriendship.Should().NotBeNull();
        removedFriendship.Should().BeSameAs(friendship);

        _friendships.Verify(x => x.Remove(friendship), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldPassCorrectUsersToRepository()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var friendUserId = Guid.NewGuid();
        var friendship = Friendship.Create(currentUserId, friendUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _friendships.Setup(x => x.GetByUsersAsync(currentUserId, friendUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(friendship);

        var handler = CreateHandler();
        var command = new RemoveFriendCommand(friendUserId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _friendships.Verify(
            x => x.GetByUsersAsync(currentUserId, friendUserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private RemoveFriendHandler CreateHandler()
        => new(
            _friendships.Object,
            _current.Object,
            _uow.Object);
}