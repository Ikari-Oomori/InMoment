using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Realtime;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Media.EditPhotoCaption;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;
using InMoment.Domain.Media;

namespace InMoment.Application.Tests.Media.EditPhotoCaption;

public sealed class EditPhotoCaptionHandlerTests
{
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IPhotoRepository> _photos = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IGroupRealtime> _realtime = new();

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenGroupIdIsEmpty()
    {
        var handler = CreateHandler();
        var command = new EditPhotoCaptionCommand(Guid.Empty, Guid.NewGuid(), "hello");

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("GroupId is required.");
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenPhotoIdIsEmpty()
    {
        var handler = CreateHandler();
        var command = new EditPhotoCaptionCommand(Guid.NewGuid(), Guid.Empty, "hello");

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("PhotoId is required.");
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUserIsNotMember()
    {
        var currentUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.IsMemberAsync(groupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateHandler();
        var command = new EditPhotoCaptionCommand(groupId, photoId, "hello");

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not an active member of this group.");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenPhotoNotFound()
    {
        var currentUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.IsMemberAsync(groupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _photos.Setup(x => x.GetByIdAsync(photoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Photo?)null);

        var handler = CreateHandler();
        var command = new EditPhotoCaptionCommand(groupId, photoId, "hello");

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Photo not found.");
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenPhotoBelongsToAnotherGroup()
    {
        var authorId = Guid.NewGuid();
        var requestedGroupId = Guid.NewGuid();
        var actualGroupId = Guid.NewGuid();

        var photo = Photo.Create(
            actualGroupId,
            authorId,
            $"groups/{actualGroupId}/photos/{authorId}/file.jpg",
            "image/jpeg",
            100,
            "old");

        _current.SetupGet(x => x.UserId).Returns(authorId);
        _groups.Setup(x => x.IsMemberAsync(requestedGroupId, authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);

        var handler = CreateHandler();
        var command = new EditPhotoCaptionCommand(requestedGroupId, photo.Id, "new");

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Photo does not belong to this group.");
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenActorIsNotAuthor()
    {
        var ownerId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var group = Group.Create("Test group", ownerId);
        group.AddMember(authorId);
        group.AddMember(adminId);
        group.PromoteToAdmin(ownerId, adminId);

        var photo = Photo.Create(
            group.Id,
            authorId,
            $"groups/{group.Id}/photos/{authorId}/file.jpg",
            "image/jpeg",
            100,
            "old");

        _current.SetupGet(x => x.UserId).Returns(adminId);
        _groups.Setup(x => x.IsMemberAsync(group.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);

        var handler = CreateHandler();
        var command = new EditPhotoCaptionCommand(group.Id, photo.Id, "new");

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not allowed to edit this photo.");

        photo.Caption.Should().Be("old");
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldEditCaption_WhenActorIsAuthor()
    {
        var ownerId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var group = Group.Create("Test group", ownerId);
        group.AddMember(authorId);

        var photo = Photo.Create(
            group.Id,
            authorId,
            $"groups/{group.Id}/photos/{authorId}/file.jpg",
            "image/jpeg",
            100,
            "old");

        _current.SetupGet(x => x.UserId).Returns(authorId);
        _groups.Setup(x => x.IsMemberAsync(group.Id, authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);

        var handler = CreateHandler();
        var command = new EditPhotoCaptionCommand(group.Id, photo.Id, "new caption");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().Be(photo.Id);
        photo.Caption.Should().Be("new caption");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _realtime.Verify(
            x => x.NotifyFeedChangedAsync(group.Id, "photo_updated", photo.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldAllowClearingCaption_WhenActorIsAuthor()
    {
        var ownerId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var group = Group.Create("Test group", ownerId);
        group.AddMember(authorId);

        var photo = Photo.Create(
            group.Id,
            authorId,
            $"groups/{group.Id}/photos/{authorId}/file.jpg",
            "image/jpeg",
            100,
            "old caption");

        _current.SetupGet(x => x.UserId).Returns(authorId);
        _groups.Setup(x => x.IsMemberAsync(group.Id, authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);

        var handler = CreateHandler();
        var command = new EditPhotoCaptionCommand(group.Id, photo.Id, "   ");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().Be(photo.Id);
        photo.Caption.Should().BeNull();

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _realtime.Verify(
            x => x.NotifyFeedChangedAsync(group.Id, "photo_updated", photo.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private EditPhotoCaptionHandler CreateHandler()
        => new(
            _current.Object,
            _groups.Object,
            _photos.Object,
            _uow.Object,
            _realtime.Object);
}