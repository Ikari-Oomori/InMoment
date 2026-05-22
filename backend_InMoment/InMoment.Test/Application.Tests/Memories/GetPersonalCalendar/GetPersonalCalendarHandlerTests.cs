using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Abstractions.Storage;
using InMoment.Application.Features.Memories.GetPersonalCalendar;
using InMoment.Domain.Common;
using InMoment.Domain.Media;

namespace InMoment.Application.Tests.Memories.GetPersonalCalendar;

public sealed class GetPersonalCalendarHandlerTests
{
    private readonly Mock<IPhotoRepository> _photos = new();
    private readonly Mock<IFileStorage> _storage = new();
    private readonly Mock<ICurrentUser> _current = new();

    private GetPersonalCalendarHandler Create()
        => new(
            _photos.Object,
            _storage.Object,
            _current.Object);

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenCurrentUserIsEmpty()
    {
        _current.SetupGet(x => x.UserId).Returns(Guid.Empty);

        var handler = Create();

        var act = () => handler.Handle(new GetPersonalCalendarQuery(2026, 4), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Пользователь не авторизован.");

        _photos.Verify(x => x.GetByUserAndDateRangeAsync(
            It.IsAny<Guid>(),
            It.IsAny<DateTime>(),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyCalendar_WhenNoPhotos()
    {
        var currentUserId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);

        _photos.Setup(x => x.GetByUserAndDateRangeAsync(
                currentUserId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Photo>());

        var handler = Create();

        var result = await handler.Handle(new GetPersonalCalendarQuery(2026, 4), CancellationToken.None);

        result.Year.Should().Be(2026);
        result.Month.Should().Be(4);
        result.Days.Should().HaveCount(30);
        result.Days.Should().OnlyContain(x => !x.HasPhotos && x.PhotosCount == 0 && x.PreviewPhotoUrl == null);

        _storage.Verify(x => x.GetPublicUrl(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldMarkDaysWithPhotos_AndSetPreviewUrl()
    {
        var currentUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var olderPhoto = Photo.Create(groupId, currentUserId, "photos/2026-04-10-old.jpg", "image/jpeg", 100);
        var newerPhoto = Photo.Create(groupId, currentUserId, "photos/2026-04-10-new.jpg", "image/jpeg", 100);
        var anotherDay = Photo.Create(groupId, currentUserId, "photos/2026-04-12.jpg", "image/jpeg", 100);

        SetCreatedAt(olderPhoto, new DateTime(2026, 4, 10, 8, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(newerPhoto, new DateTime(2026, 4, 10, 21, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(anotherDay, new DateTime(2026, 4, 12, 9, 30, 0, DateTimeKind.Utc));

        _current.SetupGet(x => x.UserId).Returns(currentUserId);

        _photos.Setup(x => x.GetByUserAndDateRangeAsync(
                currentUserId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { olderPhoto, newerPhoto, anotherDay });

        _storage.Setup(x => x.GetPublicUrl("photos/2026-04-10-new.jpg"))
            .Returns("https://cdn.example.com/photos/2026-04-10-new.jpg");

        _storage.Setup(x => x.GetPublicUrl("photos/2026-04-12.jpg"))
            .Returns("https://cdn.example.com/photos/2026-04-12.jpg");

        var handler = Create();

        var result = await handler.Handle(new GetPersonalCalendarQuery(2026, 4), CancellationToken.None);

        result.Days.Should().HaveCount(30);

        var day10 = result.Days.Single(x => x.Day == 10);
        day10.HasPhotos.Should().BeTrue();
        day10.PhotosCount.Should().Be(2);
        day10.PreviewPhotoUrl.Should().Be("https://cdn.example.com/photos/2026-04-10-new.jpg");

        var day12 = result.Days.Single(x => x.Day == 12);
        day12.HasPhotos.Should().BeTrue();
        day12.PhotosCount.Should().Be(1);
        day12.PreviewPhotoUrl.Should().Be("https://cdn.example.com/photos/2026-04-12.jpg");

        var day11 = result.Days.Single(x => x.Day == 11);
        day11.HasPhotos.Should().BeFalse();
        day11.PhotosCount.Should().Be(0);
        day11.PreviewPhotoUrl.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldUseNewestPhotoOfDayAsPreview()
    {
        var currentUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var first = Photo.Create(groupId, currentUserId, "photos/day20-first.jpg", "image/jpeg", 100);
        var second = Photo.Create(groupId, currentUserId, "photos/day20-second.jpg", "image/jpeg", 100);
        var third = Photo.Create(groupId, currentUserId, "photos/day20-third.jpg", "image/jpeg", 100);

        SetCreatedAt(first, new DateTime(2026, 4, 20, 8, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(second, new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(third, new DateTime(2026, 4, 20, 23, 0, 0, DateTimeKind.Utc));

        _current.SetupGet(x => x.UserId).Returns(currentUserId);

        _photos.Setup(x => x.GetByUserAndDateRangeAsync(
                currentUserId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { first, second, third });

        _storage.Setup(x => x.GetPublicUrl("photos/day20-third.jpg"))
            .Returns("https://cdn.example.com/photos/day20-third.jpg");

        var handler = Create();

        var result = await handler.Handle(new GetPersonalCalendarQuery(2026, 4), CancellationToken.None);

        var day20 = result.Days.Single(x => x.Day == 20);
        day20.HasPhotos.Should().BeTrue();
        day20.PhotosCount.Should().Be(3);
        day20.PreviewPhotoUrl.Should().Be("https://cdn.example.com/photos/day20-third.jpg");
    }

    private static void SetCreatedAt(Photo photo, DateTime createdAtUtc)
    {
        typeof(Photo)
            .GetProperty(nameof(Photo.CreatedAt))!
            .SetValue(photo, createdAtUtc);
    }
}