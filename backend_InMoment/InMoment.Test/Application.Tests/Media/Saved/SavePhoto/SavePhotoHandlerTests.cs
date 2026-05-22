using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Media.Saved.SavePhoto;
using InMoment.Domain.Common;
using InMoment.Domain.Media;
using Moq;

namespace InMoment.Application.Tests.Media.Saved.SavePhoto;

public sealed class SavePhotoHandlerTests
{
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IPhotoRepository> _photos = new();
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<ISavedPhotoRepository> _saved = new();
    private readonly Mock<IBlockedUserRepository> _blocks = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private SavePhotoHandler Create()
        => new(
            _current.Object,
            _photos.Object,
            _groups.Object,
            _saved.Object,
            _blocks.Object,
            _uow.Object);

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenPhotoIdEmpty()
    {
        var handler = Create();

        var act = () => handler.Handle(new SavePhotoCommand(Guid.Empty), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("PhotoId is required.");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenPhotoMissing()
    {
        _photos.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Photo?)null);

        var handler = Create();

        var act = () => handler.Handle(new SavePhotoCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Photo not found.");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenPhotoDeleted()
    {
        var photo = Photo.Create(Guid.NewGuid(), Guid.NewGuid(), "key", "image/jpeg", 100);
        photo.MarkDeleted(photo.UploadedByUserId, photo.UploadedByUserId);

        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);

        var handler = Create();

        var act = () => handler.Handle(new SavePhotoCommand(photo.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Photo not found.");
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUserNotMember()
    {
        var currentUserId = Guid.NewGuid();
        var photo = Photo.Create(Guid.NewGuid(), Guid.NewGuid(), "key", "image/jpeg", 100);

        _current.Setup(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = Create();

        var act = () => handler.Handle(new SavePhotoCommand(photo.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not an active member of this group.");
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenBlockedEitherDirection()
    {
        var currentUserId = Guid.NewGuid();
        var photo = Photo.Create(Guid.NewGuid(), Guid.NewGuid(), "key", "image/jpeg", 100);

        _current.Setup(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, photo.UploadedByUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = Create();

        var act = () => handler.Handle(new SavePhotoCommand(photo.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Взаимодействие с этим пользователем недоступно.");
    }

    [Fact]
    public async Task Handle_ShouldReturn_WhenSavedPhotoAlreadyExists()
    {
        var currentUserId = Guid.NewGuid();
        var photo = Photo.Create(Guid.NewGuid(), Guid.NewGuid(), "key", "image/jpeg", 100);
        var existing = SavedPhoto.Create(photo.Id, currentUserId);

        _current.Setup(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, photo.UploadedByUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _saved.Setup(x => x.GetByPhotoAndUserAsync(photo.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var handler = Create();

        await handler.Handle(new SavePhotoCommand(photo.Id), CancellationToken.None);

        _saved.Verify(x => x.AddAsync(It.IsAny<SavedPhoto>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldSavePhoto_WhenValid()
    {
        var currentUserId = Guid.NewGuid();
        var photo = Photo.Create(Guid.NewGuid(), Guid.NewGuid(), "key", "image/jpeg", 100);

        SavedPhoto? added = null;

        _current.Setup(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, photo.UploadedByUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _saved.Setup(x => x.GetByPhotoAndUserAsync(photo.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SavedPhoto?)null);
        _saved.Setup(x => x.AddAsync(It.IsAny<SavedPhoto>(), It.IsAny<CancellationToken>()))
            .Callback<SavedPhoto, CancellationToken>((x, _) => added = x)
            .Returns(Task.CompletedTask);

        var handler = Create();

        await handler.Handle(new SavePhotoCommand(photo.Id), CancellationToken.None);

        added.Should().NotBeNull();
        added!.PhotoId.Should().Be(photo.Id);
        added.UserId.Should().Be(currentUserId);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}