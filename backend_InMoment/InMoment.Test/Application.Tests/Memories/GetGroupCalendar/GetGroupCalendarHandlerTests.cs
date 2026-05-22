using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Abstractions.Storage;
using InMoment.Application.Features.Memories.GetGroupCalendar;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;
using InMoment.Domain.Media;

namespace InMoment.Application.Tests.Memories.GetGroupCalendar;

public sealed class GetGroupCalendarHandlerTests
{
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IPhotoRepository> _photos = new();
    private readonly Mock<IFileStorage> _storage = new();
    private readonly Mock<ICurrentUser> _current = new();

    private GetGroupCalendarHandler Create()
        => new(
            _groups.Object,
            _photos.Object,
            _storage.Object,
            _current.Object);

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenGroupIdIsEmpty()
    {
        _current.SetupGet(x => x.UserId).Returns(Guid.NewGuid());

        var handler = Create();

        var act = () => handler.Handle(
            new GetGroupCalendarQuery(Guid.Empty, 2026, 4),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("GroupId is required.");

        _groups.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenGroupNotFound()
    {
        var currentUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group?)null);

        var handler = Create();

        var act = () => handler.Handle(
            new GetGroupCalendarQuery(groupId, 2026, 4),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Group not found.");

        _photos.Verify(x => x.GetByGroupAndDateRangeAsync(
            It.IsAny<Guid>(),
            It.IsAny<DateTime>(),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenCurrentUserIsNotGroupMember()
    {
        var currentUserId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        var group = Group.Create("Secret", ownerId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = Create();

        var act = () => handler.Handle(
            new GetGroupCalendarQuery(group.Id, 2026, 4),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not a member of this group.");

        _photos.Verify(x => x.GetByGroupAndDateRangeAsync(
            It.IsAny<Guid>(),
            It.IsAny<DateTime>(),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyCalendar_WhenNoPhotos()
    {
        var currentUserId = Guid.NewGuid();
        var group = Group.Create("Family", currentUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _photos.Setup(x => x.GetByGroupAndDateRangeAsync(
                group.Id,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Photo>());

        var handler = Create();

        var result = await handler.Handle(
            new GetGroupCalendarQuery(group.Id, 2026, 4),
            CancellationToken.None);

        result.GroupId.Should().Be(group.Id);
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
        var group = Group.Create("Family", currentUserId);

        var p1 = Photo.Create(group.Id, currentUserId, "group/day10-old.jpg", "image/jpeg", 100);
        var p2 = Photo.Create(group.Id, currentUserId, "group/day10-new.jpg", "image/jpeg", 100);
        var p3 = Photo.Create(group.Id, currentUserId, "group/day12.jpg", "image/jpeg", 100);

        SetCreatedAt(p1, new DateTime(2026, 4, 10, 8, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(p2, new DateTime(2026, 4, 10, 22, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(p3, new DateTime(2026, 4, 12, 9, 30, 0, DateTimeKind.Utc));

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _photos.Setup(x => x.GetByGroupAndDateRangeAsync(
                group.Id,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { p1, p2, p3 });

        _storage.Setup(x => x.GetPublicUrl("group/day10-new.jpg"))
            .Returns("https://cdn.example.com/group/day10-new.jpg");

        _storage.Setup(x => x.GetPublicUrl("group/day12.jpg"))
            .Returns("https://cdn.example.com/group/day12.jpg");

        var handler = Create();

        var result = await handler.Handle(
            new GetGroupCalendarQuery(group.Id, 2026, 4),
            CancellationToken.None);

        result.Days.Should().HaveCount(30);

        var day10 = result.Days.Single(x => x.Day == 10);
        day10.HasPhotos.Should().BeTrue();
        day10.PhotosCount.Should().Be(2);
        day10.PreviewPhotoUrl.Should().Be("https://cdn.example.com/group/day10-new.jpg");

        var day12 = result.Days.Single(x => x.Day == 12);
        day12.HasPhotos.Should().BeTrue();
        day12.PhotosCount.Should().Be(1);
        day12.PreviewPhotoUrl.Should().Be("https://cdn.example.com/group/day12.jpg");
    }

    [Fact]
    public async Task Handle_ShouldUseNewestPhotoOfDayAsPreview()
    {
        var currentUserId = Guid.NewGuid();
        var group = Group.Create("Family", currentUserId);

        var first = Photo.Create(group.Id, currentUserId, "group/day20-first.jpg", "image/jpeg", 100);
        var second = Photo.Create(group.Id, currentUserId, "group/day20-second.jpg", "image/jpeg", 100);
        var third = Photo.Create(group.Id, currentUserId, "group/day20-third.jpg", "image/jpeg", 100);

        SetCreatedAt(first, new DateTime(2026, 4, 20, 8, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(second, new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(third, new DateTime(2026, 4, 20, 23, 0, 0, DateTimeKind.Utc));

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _photos.Setup(x => x.GetByGroupAndDateRangeAsync(
                group.Id,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { first, second, third });

        _storage.Setup(x => x.GetPublicUrl("group/day20-third.jpg"))
            .Returns("https://cdn.example.com/group/day20-third.jpg");

        var handler = Create();

        var result = await handler.Handle(
            new GetGroupCalendarQuery(group.Id, 2026, 4),
            CancellationToken.None);

        var day20 = result.Days.Single(x => x.Day == 20);
        day20.HasPhotos.Should().BeTrue();
        day20.PhotosCount.Should().Be(3);
        day20.PreviewPhotoUrl.Should().Be("https://cdn.example.com/group/day20-third.jpg");
    }

    private static void SetCreatedAt(Photo photo, DateTime createdAtUtc)
    {
        typeof(Photo)
            .GetProperty(nameof(Photo.CreatedAt))!
            .SetValue(photo, createdAtUtc);
    }
}