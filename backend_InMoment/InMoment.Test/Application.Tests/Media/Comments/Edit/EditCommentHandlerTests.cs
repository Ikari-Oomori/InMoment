using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Media.Comments.Edit;
using InMoment.Domain.Common;
using InMoment.Domain.Media;

namespace InMoment.Application.Tests.Media.Comments.Edit;

public sealed class EditCommentHandlerTests
{
    private readonly Mock<ICommentRepository> _comments = new();
    private readonly Mock<IPhotoRepository> _photos = new();
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _current = new();

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenCommentIdIsEmpty()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new EditCommentCommand(Guid.Empty, "updated text");

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("CommentId is required.");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenCommentNotFound()
    {
        // Arrange
        var commentId = Guid.NewGuid();

        _comments.Setup(x => x.GetByIdAsync(commentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Comment?)null);

        var handler = CreateHandler();
        var command = new EditCommentCommand(commentId, "updated text");

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
        var comment = Comment.CreateRoot(Guid.NewGuid(), Guid.NewGuid(), "original");

        _comments.Setup(x => x.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);
        _photos.Setup(x => x.GetByIdAsync(comment.PhotoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Photo?)null);

        var handler = CreateHandler();
        var command = new EditCommentCommand(comment.Id, "updated text");

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
        var comment = Comment.CreateRoot(photo.Id, commentUserId, "original");

        photo.MarkDeleted(ownerUserId, ownerUserId);

        _comments.Setup(x => x.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);
        _photos.Setup(x => x.GetByIdAsync(comment.PhotoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);

        var handler = CreateHandler();
        var command = new EditCommentCommand(comment.Id, "updated text");

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
        var comment = Comment.CreateRoot(photo.Id, commentUserId, "original");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _comments.Setup(x => x.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);
        _photos.Setup(x => x.GetByIdAsync(comment.PhotoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateHandler();
        var command = new EditCommentCommand(comment.Id, "updated text");

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not an active member of this group.");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenCurrentUserIsNotCommentAuthor()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var commentUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), commentUserId, ownerUserId);
        var comment = Comment.CreateRoot(photo.Id, commentUserId, "original");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _comments.Setup(x => x.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);
        _photos.Setup(x => x.GetByIdAsync(comment.PhotoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler();
        var command = new EditCommentCommand(comment.Id, "updated text");

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not allowed to edit this comment.");

        comment.Text.Should().Be("original");
        comment.EditedAt.Should().BeNull();
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenCommentTextInvalid()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, ownerUserId);
        var comment = Comment.CreateRoot(photo.Id, currentUserId, "original");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _comments.Setup(x => x.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);
        _photos.Setup(x => x.GetByIdAsync(comment.PhotoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler();
        var command = new EditCommentCommand(comment.Id, "   ");

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Comment text must be 1..500 characters.");

        comment.Text.Should().Be("original");
        comment.EditedAt.Should().BeNull();
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldEditComment_WhenValid()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, ownerUserId);
        var comment = Comment.CreateRoot(photo.Id, currentUserId, "original");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _comments.Setup(x => x.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);
        _photos.Setup(x => x.GetByIdAsync(comment.PhotoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler();
        var command = new EditCommentCommand(comment.Id, "updated text");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(comment.Id);
        comment.Text.Should().Be("updated text");
        comment.EditedAt.Should().NotBeNull();

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldSave_WhenTextIsSame()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var photo = CreatePhoto(Guid.NewGuid(), currentUserId, ownerUserId);
        var comment = Comment.CreateRoot(photo.Id, currentUserId, "same text");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _comments.Setup(x => x.GetByIdAsync(comment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);
        _photos.Setup(x => x.GetByIdAsync(comment.PhotoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler();
        var command = new EditCommentCommand(comment.Id, "same text");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(comment.Id);
        comment.Text.Should().Be("same text");
        comment.EditedAt.Should().BeNull();

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private EditCommentHandler CreateHandler()
        => new(
            _comments.Object,
            _photos.Object,
            _groups.Object,
            _uow.Object,
            _current.Object);

    private static Photo CreatePhoto(Guid groupId, Guid uploadedByUserId, Guid groupOwnerId)
        => Photo.Create(
            groupId: groupId,
            uploadedByUserId: uploadedByUserId,
            storageKey: $"groups/{groupId}/photos/{uploadedByUserId}/{groupOwnerId}.jpg",
            contentType: "image/jpeg",
            sizeBytes: 1024);
}