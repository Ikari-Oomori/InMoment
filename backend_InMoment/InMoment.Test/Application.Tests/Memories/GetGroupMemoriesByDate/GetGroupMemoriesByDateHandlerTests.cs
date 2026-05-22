using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Abstractions.Storage;
using InMoment.Application.Features.Memories.GetGroupMemoriesByDate;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;
using InMoment.Domain.Media;

namespace InMoment.Application.Tests.Memories.GetGroupMemoriesByDate;

public sealed class GetGroupMemoriesByDateHandlerTests
{
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IPhotoRepository> _photos = new();
    private readonly Mock<IFileStorage> _storage = new();
    private readonly Mock<ICurrentUser> _current = new();

    private GetGroupMemoriesByDateHandler Create()
        => new(_groups.Object, _photos.Object, _storage.Object, _current.Object);

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenGroupIdEmpty()
    {
        var handler = Create();

        var act = () => handler.Handle(
            new GetGroupMemoriesByDateQuery(Guid.Empty, new DateOnly(2026, 4, 1)),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("GroupId is required.");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenGroupNotFound()
    {
        var groupId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group?)null);

        var handler = Create();

        var act = () => handler.Handle(
            new GetGroupMemoriesByDateQuery(groupId, new DateOnly(2026, 4, 1)),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Group not found.");
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenCurrentUserNotMember()
    {
        var ownerId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var group = Group.Create("Family", ownerId);

        _current.SetupGet(x => x.UserId).Returns(outsiderId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = Create();

        var act = () => handler.Handle(
            new GetGroupMemoriesByDateQuery(group.Id, new DateOnly(2026, 4, 1)),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not a member of this group.");
    }

    [Fact]
    public async Task Handle_ShouldReturnOnlyNotDeletedPhotos_OrderedByCreatedAtDesc()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var date = new DateOnly(2026, 4, 1);

        var group = Group.Create("Family", ownerId);
        group.AddMember(memberId);

        var first = Photo.Create(group.Id, memberId, "photos/1.jpg", "image/jpeg", 100);
        await Task.Delay(5);
        var second = Photo.Create(group.Id, memberId, "photos/2.jpg", "image/jpeg", 100);
        await Task.Delay(5);
        var deleted = Photo.Create(group.Id, memberId, "photos/3.jpg", "image/jpeg", 100);
        deleted.MarkDeleted(memberId, ownerId);

        _current.SetupGet(x => x.UserId).Returns(memberId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _photos.Setup(x => x.GetByGroupAndDateRangeAsync(
                group.Id,
                date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(1),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { first, deleted, second });

        _storage.Setup(x => x.GetPublicUrl("photos/1.jpg")).Returns("https://cdn.example.com/photos/1.jpg");
        _storage.Setup(x => x.GetPublicUrl("photos/2.jpg")).Returns("https://cdn.example.com/photos/2.jpg");

        var handler = Create();

        var result = await handler.Handle(
            new GetGroupMemoriesByDateQuery(group.Id, date),
            CancellationToken.None);

        result.GroupId.Should().Be(group.Id);
        result.Date.Should().Be(date);
        result.Items.Should().HaveCount(2);

        result.Items[0].PhotoId.Should().Be(second.Id);
        result.Items[0].PhotoUrl.Should().Be("https://cdn.example.com/photos/2.jpg");
        result.Items[0].UploadedByUserId.Should().Be(memberId);

        result.Items[1].PhotoId.Should().Be(first.Id);
        result.Items[1].PhotoUrl.Should().Be("https://cdn.example.com/photos/1.jpg");
        result.Items[1].UploadedByUserId.Should().Be(memberId);
    }
}