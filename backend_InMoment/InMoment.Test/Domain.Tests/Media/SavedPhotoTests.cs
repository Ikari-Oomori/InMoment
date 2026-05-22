using FluentAssertions;
using InMoment.Domain.Common;
using InMoment.Domain.Media;

namespace InMoment.Domain.Tests.Media;

public sealed class SavedPhotoTests
{
    [Fact]
    public void Create_ShouldThrowValidationException_WhenPhotoIdEmpty()
    {
        var act = () => SavedPhoto.Create(Guid.Empty, Guid.NewGuid());

        act.Should().Throw<ValidationException>()
            .WithMessage("PhotoId is required.");
    }

    [Fact]
    public void Create_ShouldThrowValidationException_WhenUserIdEmpty()
    {
        var act = () => SavedPhoto.Create(Guid.NewGuid(), Guid.Empty);

        act.Should().Throw<ValidationException>()
            .WithMessage("UserId is required.");
    }

    [Fact]
    public void Create_ShouldCreateSavedPhoto_WhenValid()
    {
        var photoId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var result = SavedPhoto.Create(photoId, userId);

        result.Id.Should().NotBe(Guid.Empty);
        result.PhotoId.Should().Be(photoId);
        result.UserId.Should().Be(userId);
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}