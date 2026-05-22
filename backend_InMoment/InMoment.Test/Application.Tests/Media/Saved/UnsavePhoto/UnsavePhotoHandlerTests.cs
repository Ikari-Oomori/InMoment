using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Media.Saved.UnsavePhoto;
using InMoment.Domain.Common;
using InMoment.Domain.Media;
using Moq;

namespace InMoment.Application.Tests.Media.Saved.UnsavePhoto;

public sealed class UnsavePhotoHandlerTests
{
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<ISavedPhotoRepository> _saved = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private UnsavePhotoHandler Create()
        => new(_current.Object, _saved.Object, _uow.Object);

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenPhotoIdEmpty()
    {
        var handler = Create();

        var act = () => handler.Handle(new UnsavePhotoCommand(Guid.Empty), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("PhotoId is required.");
    }

    [Fact]
    public async Task Handle_ShouldReturn_WhenSavedPhotoMissing()
    {
        var currentUserId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        _current.Setup(x => x.UserId).Returns(currentUserId);
        _saved.Setup(x => x.GetByPhotoAndUserAsync(photoId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SavedPhoto?)null);

        var handler = Create();

        await handler.Handle(new UnsavePhotoCommand(photoId), CancellationToken.None);

        _saved.Verify(x => x.RemoveAsync(It.IsAny<SavedPhoto>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRemoveSavedPhoto_WhenExists()
    {
        var currentUserId = Guid.NewGuid();
        var photoId = Guid.NewGuid();
        var existing = SavedPhoto.Create(photoId, currentUserId);

        _current.Setup(x => x.UserId).Returns(currentUserId);
        _saved.Setup(x => x.GetByPhotoAndUserAsync(photoId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var handler = Create();

        await handler.Handle(new UnsavePhotoCommand(photoId), CancellationToken.None);

        _saved.Verify(x => x.RemoveAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}