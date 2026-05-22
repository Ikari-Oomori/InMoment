using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Realtime;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Media.DeletePhoto;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;
using InMoment.Domain.Media;

namespace InMoment.Application.Tests.Media.DeletePhoto;

public sealed class DeletePhotoHandlerTests
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
        var command = new DeletePhotoCommand(Guid.Empty, Guid.NewGuid());

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("GroupId is required.");
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenPhotoIdIsEmpty()
    {
        var handler = CreateHandler();
        var command = new DeletePhotoCommand(Guid.NewGuid(), Guid.Empty);

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("PhotoId is required.");
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUserIsNotGroupMember()
    {
        var currentUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.IsMemberAsync(groupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateHandler();
        var command = new DeletePhotoCommand(groupId, photoId);

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not an active member of this group.");

        _groups.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _photos.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _realtime.Verify(
            x => x.NotifyFeedChangedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenGroupNotFound()
    {
        var currentUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.IsMemberAsync(groupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _groups.Setup(x => x.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group?)null);

        var handler = CreateHandler();
        var command = new DeletePhotoCommand(groupId, photoId);

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Group not found.");

        _photos.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenPhotoNotFound()
    {
        var currentUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var group = Group.Create("Test group", ownerUserId);
        group.AddMember(currentUserId);
        var photoId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.IsMemberAsync(group.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _photos.Setup(x => x.GetByIdAsync(photoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Photo?)null);

        var handler = CreateHandler();
        var command = new DeletePhotoCommand(group.Id, photoId);

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Photo not found.");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _realtime.Verify(
            x => x.NotifyFeedChangedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenPhotoDoesNotBelongToGroup()
    {
        var currentUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var group = Group.Create("Test group", ownerUserId);
        group.AddMember(currentUserId);

        var otherGroupId = Guid.NewGuid();
        var photo = Photo.Create(
            groupId: otherGroupId,
            uploadedByUserId: currentUserId,
            storageKey: $"groups/{otherGroupId}/photos/{currentUserId}/file.jpg",
            contentType: "image/jpeg",
            sizeBytes: 100);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.IsMemberAsync(group.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);

        var handler = CreateHandler();
        var command = new DeletePhotoCommand(group.Id, photo.Id);

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Photo does not belong to this group.");

        photo.IsDeleted.Should().BeFalse();
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnWithoutSaving_WhenPhotoAlreadyDeleted()
    {
        var ownerUserId = Guid.NewGuid();
        var group = Group.Create("Test group", ownerUserId);

        var photo = Photo.Create(
            groupId: group.Id,
            uploadedByUserId: ownerUserId,
            storageKey: $"groups/{group.Id}/photos/{ownerUserId}/file.jpg",
            contentType: "image/jpeg",
            sizeBytes: 100);

        photo.MarkDeleted(ownerUserId, group.OwnerId);

        _current.SetupGet(x => x.UserId).Returns(ownerUserId);
        _groups.Setup(x => x.IsMemberAsync(group.Id, ownerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);

        var handler = CreateHandler();
        var command = new DeletePhotoCommand(group.Id, photo.Id);

        await handler.Handle(command, CancellationToken.None);

        photo.IsDeleted.Should().BeTrue();
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _realtime.Verify(
            x => x.NotifyFeedChangedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldDeletePhoto_WhenRequestedByUploader()
    {
        var ownerUserId = Guid.NewGuid();
        var uploaderUserId = Guid.NewGuid();
        var group = Group.Create("Test group", ownerUserId);
        group.AddMember(uploaderUserId);

        var photo = Photo.Create(
            groupId: group.Id,
            uploadedByUserId: uploaderUserId,
            storageKey: $"groups/{group.Id}/photos/{uploaderUserId}/file.jpg",
            contentType: "image/jpeg",
            sizeBytes: 100);

        _current.SetupGet(x => x.UserId).Returns(uploaderUserId);
        _groups.Setup(x => x.IsMemberAsync(group.Id, uploaderUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);

        var handler = CreateHandler();
        var command = new DeletePhotoCommand(group.Id, photo.Id);

        await handler.Handle(command, CancellationToken.None);

        photo.IsDeleted.Should().BeTrue();

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _realtime.Verify(
            x => x.NotifyFeedChangedAsync(group.Id, "photo_deleted", photo.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldDeletePhoto_WhenRequestedByGroupOwner()
    {
        var ownerUserId = Guid.NewGuid();
        var uploaderUserId = Guid.NewGuid();
        var group = Group.Create("Test group", ownerUserId);
        group.AddMember(uploaderUserId);

        var photo = Photo.Create(
            groupId: group.Id,
            uploadedByUserId: uploaderUserId,
            storageKey: $"groups/{group.Id}/photos/{uploaderUserId}/file.jpg",
            contentType: "image/jpeg",
            sizeBytes: 100);

        _current.SetupGet(x => x.UserId).Returns(ownerUserId);
        _groups.Setup(x => x.IsMemberAsync(group.Id, ownerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);

        var handler = CreateHandler();
        var command = new DeletePhotoCommand(group.Id, photo.Id);

        await handler.Handle(command, CancellationToken.None);

        photo.IsDeleted.Should().BeTrue();

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _realtime.Verify(
            x => x.NotifyFeedChangedAsync(group.Id, "photo_deleted", photo.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldDeletePhoto_WhenRequestedByGroupAdmin()
    {
        var ownerUserId = Guid.NewGuid();
        var uploaderUserId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();

        var group = Group.Create("Test group", ownerUserId);
        group.AddMember(uploaderUserId);
        group.AddMember(adminUserId);
        group.PromoteToAdmin(ownerUserId, adminUserId);

        var photo = Photo.Create(
            groupId: group.Id,
            uploadedByUserId: uploaderUserId,
            storageKey: $"groups/{group.Id}/photos/{uploaderUserId}/file.jpg",
            contentType: "image/jpeg",
            sizeBytes: 100);

        _current.SetupGet(x => x.UserId).Returns(adminUserId);
        _groups.Setup(x => x.IsMemberAsync(group.Id, adminUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);

        var handler = CreateHandler();
        var command = new DeletePhotoCommand(group.Id, photo.Id);

        await handler.Handle(command, CancellationToken.None);

        photo.IsDeleted.Should().BeTrue();

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _realtime.Verify(
            x => x.NotifyFeedChangedAsync(group.Id, "photo_deleted", photo.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenMemberIsNeitherUploaderNorManager()
    {
        var ownerUserId = Guid.NewGuid();
        var uploaderUserId = Guid.NewGuid();
        var otherMemberUserId = Guid.NewGuid();

        var group = Group.Create("Test group", ownerUserId);
        group.AddMember(uploaderUserId);
        group.AddMember(otherMemberUserId);

        var photo = Photo.Create(
            groupId: group.Id,
            uploadedByUserId: uploaderUserId,
            storageKey: $"groups/{group.Id}/photos/{uploaderUserId}/file.jpg",
            contentType: "image/jpeg",
            sizeBytes: 100);

        _current.SetupGet(x => x.UserId).Returns(otherMemberUserId);
        _groups.Setup(x => x.IsMemberAsync(group.Id, otherMemberUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);

        var handler = CreateHandler();
        var command = new DeletePhotoCommand(group.Id, photo.Id);

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not allowed to delete this photo.");

        photo.IsDeleted.Should().BeFalse();
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _realtime.Verify(
            x => x.NotifyFeedChangedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private DeletePhotoHandler CreateHandler()
        => new(
            _current.Object,
            _groups.Object,
            _photos.Object,
            _uow.Object,
            _realtime.Object);
}