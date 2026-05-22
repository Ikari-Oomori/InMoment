using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Realtime;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Media.Comments.Delete;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;
using InMoment.Domain.Media;

namespace InMoment.Application.Tests.Media.Comments.Delete;

public sealed class DeleteCommentHandlerTests
{
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<ICommentRepository> _comments = new();
    private readonly Mock<IPhotoRepository> _photos = new();
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IGroupRealtime> _realtime = new();

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenCommentNotFound()
    {
        // Arrange
        var commentId = Guid.NewGuid();

        _comments.Setup(x => x.GetByIdAsync(commentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Comment?)null);

        var handler = CreateHandler();
        var command = new DeleteCommentCommand(commentId);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Comment not found.");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenPhotoNotFound()
    {
        // Arrange
        var comment = Comment.CreateRoot(Guid.NewGuid(), Guid.NewGuid(), "text");

        _comments.Setup(x => x.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);
        _photos.Setup(x => x.GetByIdAsync(comment.PhotoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Photo?)null);

        var handler = CreateHandler();
        var command = new DeleteCommentCommand(comment.Id);

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
        var commentUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), commentUserId, ownerUserId);
        var comment = Comment.CreateRoot(photo.Id, commentUserId, "text");

        photo.MarkDeleted(ownerUserId, ownerUserId);

        _comments.Setup(x => x.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);
        _photos.Setup(x => x.GetByIdAsync(comment.PhotoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);

        var handler = CreateHandler();
        var command = new DeleteCommentCommand(comment.Id);

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
        var commentUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), commentUserId, ownerUserId);
        var comment = Comment.CreateRoot(photo.Id, commentUserId, "text");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _comments.Setup(x => x.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);
        _photos.Setup(x => x.GetByIdAsync(comment.PhotoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateHandler();
        var command = new DeleteCommentCommand(comment.Id);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not an active member of this group.");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _realtime.Verify(
            x => x.NotifyFeedChangedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldDeleteComment_WhenCurrentUserIsAuthor()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, ownerUserId);
        var comment = Comment.CreateRoot(photo.Id, currentUserId, "text");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _comments.Setup(x => x.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);
        _photos.Setup(x => x.GetByIdAsync(comment.PhotoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler();
        var command = new DeleteCommentCommand(comment.Id);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        comment.IsDeleted.Should().BeTrue();

        _groups.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _realtime.Verify(
            x => x.NotifyFeedChangedAsync(photo.GroupId, "comment_changed", photo.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenGroupNotFound_ForOwnerBranch()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var commentUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), commentUserId, Guid.NewGuid());
        var comment = Comment.CreateRoot(photo.Id, commentUserId, "text");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _comments.Setup(x => x.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);
        _photos.Setup(x => x.GetByIdAsync(comment.PhotoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _groups.Setup(x => x.GetByIdAsync(photo.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group?)null);

        var handler = CreateHandler();
        var command = new DeleteCommentCommand(comment.Id);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Group not found.");

        comment.IsDeleted.Should().BeFalse();
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenCurrentUserIsNotAuthorAndNotOwner()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var commentUserId = Guid.NewGuid();
        var otherMemberUserId = Guid.NewGuid();

        var group = Group.Create("Test group", ownerUserId);
        group.AddMember(commentUserId);
        group.AddMember(otherMemberUserId);

        var photo = Photo.Create(
            groupId: group.Id,
            uploadedByUserId: commentUserId,
            storageKey: $"groups/{group.Id}/photos/{commentUserId}/{ownerUserId}.jpg",
            contentType: "image/jpeg",
            sizeBytes: 1024);

        var comment = Comment.CreateRoot(photo.Id, commentUserId, "text");

        _current.SetupGet(x => x.UserId).Returns(otherMemberUserId);
        _comments.Setup(x => x.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);
        _photos.Setup(x => x.GetByIdAsync(comment.PhotoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, otherMemberUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _groups.Setup(x => x.GetByIdAsync(photo.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = CreateHandler();
        var command = new DeleteCommentCommand(comment.Id);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You cannot delete this comment.");

        comment.IsDeleted.Should().BeFalse();
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _realtime.Verify(
            x => x.NotifyFeedChangedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldDeleteComment_WhenCurrentUserIsGroupOwner()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var commentUserId = Guid.NewGuid();

        var group = Group.Create("Test group", ownerUserId);
        group.AddMember(commentUserId);

        var photo = Photo.Create(
            groupId: group.Id,
            uploadedByUserId: commentUserId,
            storageKey: $"groups/{group.Id}/photos/{commentUserId}/{ownerUserId}.jpg",
            contentType: "image/jpeg",
            sizeBytes: 1024);

        var comment = Comment.CreateRoot(photo.Id, commentUserId, "text");

        _current.SetupGet(x => x.UserId).Returns(ownerUserId);
        _comments.Setup(x => x.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);
        _photos.Setup(x => x.GetByIdAsync(comment.PhotoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, ownerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _groups.Setup(x => x.GetByIdAsync(photo.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = CreateHandler();
        var command = new DeleteCommentCommand(comment.Id);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        comment.IsDeleted.Should().BeTrue();

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _realtime.Verify(
            x => x.NotifyFeedChangedAsync(photo.GroupId, "comment_changed", photo.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private DeleteCommentHandler CreateHandler()
        => new(
            _current.Object,
            _comments.Object,
            _photos.Object,
            _groups.Object,
            _uow.Object,
            _realtime.Object);

    private static Photo CreatePhoto(Guid groupId, Guid uploadedByUserId, Guid groupOwnerId)
        => Photo.Create(
            groupId: groupId,
            uploadedByUserId: uploadedByUserId,
            storageKey: $"groups/{groupId}/photos/{uploadedByUserId}/{groupOwnerId}.jpg",
            contentType: "image/jpeg",
            sizeBytes: 1024);
}