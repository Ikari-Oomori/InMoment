using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Realtime;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Media.Reactions.Remove;
using InMoment.Application.Features.Media.Reactions.RemoveReaction;
using InMoment.Domain.Common;
using InMoment.Domain.Media;

namespace InMoment.Application.Tests.Media.Reactions.RemoveReaction;

public sealed class RemoveReactionHandlerTests
{
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IReactionRepository> _reactions = new();
    private readonly Mock<IPhotoRepository> _photos = new();
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IBlockedUserRepository> _blocks = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IGroupRealtime> _realtime = new();

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenPhotoNotFound()
    {
        // Arrange
        var photoId = Guid.NewGuid();

        _photos.Setup(x => x.GetByIdAsync(photoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Photo?)null);

        var handler = CreateHandler();
        var command = new RemoveReactionCommand(photoId);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Photo not found.");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenPhotoIsDeleted()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), ownerUserId, ownerUserId);
        photo.MarkDeleted(ownerUserId, ownerUserId);

        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);

        var handler = CreateHandler();
        var command = new RemoveReactionCommand(photo.Id);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Photo not found.");
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUserIsNotGroupMember()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var uploaderUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, uploaderUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateHandler();
        var command = new RemoveReactionCommand(photo.Id);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not an active member of this group.");

        _reactions.Verify(x => x.RemoveAsync(It.IsAny<Reaction>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _realtime.Verify(
            x => x.NotifyFeedChangedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUsersAreBlocked()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var uploaderUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, uploaderUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, uploaderUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler();
        var command = new RemoveReactionCommand(photo.Id);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Взаимодействие с этим пользователем недоступно.");

        _reactions.Verify(x => x.RemoveAsync(It.IsAny<Reaction>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _realtime.Verify(
            x => x.NotifyFeedChangedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnWithoutSaving_WhenReactionDoesNotExist()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var uploaderUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, uploaderUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, uploaderUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _reactions.Setup(x => x.GetByPhotoAndUserAsync(photo.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Reaction?)null);

        var handler = CreateHandler();
        var command = new RemoveReactionCommand(photo.Id);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _reactions.Verify(x => x.RemoveAsync(It.IsAny<Reaction>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _realtime.Verify(
            x => x.NotifyFeedChangedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRemoveReaction_SaveAndNotify_WhenReactionExists()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var uploaderUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, uploaderUserId);
        var reaction = Reaction.Create(photo.Id, currentUserId, ReactionType.Heart);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, uploaderUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _reactions.Setup(x => x.GetByPhotoAndUserAsync(photo.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reaction);

        Reaction? removedReaction = null;
        _reactions.Setup(x => x.RemoveAsync(It.IsAny<Reaction>(), It.IsAny<CancellationToken>()))
            .Callback<Reaction, CancellationToken>((r, _) => removedReaction = r)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var command = new RemoveReactionCommand(photo.Id);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        removedReaction.Should().BeSameAs(reaction);

        _reactions.Verify(x => x.RemoveAsync(reaction, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _realtime.Verify(
            x => x.NotifyFeedChangedAsync(photo.GroupId, "reaction_changed", photo.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private RemoveReactionHandler CreateHandler()
        => new(
            _current.Object,
            _reactions.Object,
            _photos.Object,
            _groups.Object,
            _blocks.Object,
            _uow.Object,
            _realtime.Object);

    private static Photo CreatePhoto(Guid groupId, Guid currentUserId, Guid uploadedByUserId)
        => Photo.Create(
            groupId: groupId,
            uploadedByUserId: uploadedByUserId,
            storageKey: $"groups/{groupId}/photos/{uploadedByUserId}/{currentUserId}.jpg",
            contentType: "image/jpeg",
            sizeBytes: 1024);
}