using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Abstractions.Storage;
using InMoment.Application.Features.Users.GetWidget;
using InMoment.Domain.Groups;
using InMoment.Domain.Media;
using InMoment.Domain.Users;

namespace InMoment.Application.Tests.Users.GetWidget;

public sealed class GetWidgetHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IPhotoRepository> _photos = new();
    private readonly Mock<INotificationRepository> _notifications = new();
    private readonly Mock<IFileStorage> _storage = new();
    private readonly Mock<ICurrentUser> _current = new();

    private GetWidgetHandler Create()
        => new(
            _users.Object,
            _groups.Object,
            _photos.Object,
            _notifications.Object,
            _storage.Object,
            _current.Object);

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenUserNotFound()
    {
        var currentUserId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = Create();

        var act = () => handler.Handle(new GetWidgetQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<InMoment.Domain.Common.NotFoundException>()
            .WithMessage("User not found.");
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyWidget_WhenActiveGroupIsNotSet()
    {
        var currentUserId = Guid.NewGuid();
        var user = User.Create(
            "widget-empty@test.com",
            "hash",
            "widget_empty",
            "Widget",
            "Empty");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = Create();

        var result = await handler.Handle(new GetWidgetQuery(), CancellationToken.None);

        result.ActiveGroupId.Should().BeNull();
        result.ActiveGroupName.Should().BeNull();
        result.ActiveGroupAvatarUrl.Should().BeNull();
        result.LatestPhotoId.Should().BeNull();
        result.LatestPhotoUrl.Should().BeNull();
        result.LatestPhotoCreatedAt.Should().BeNull();
        result.NewReactionsCount.Should().Be(0);

        _groups.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _photos.Verify(x => x.GetLatestByGroupAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _notifications.Verify(x => x.GetUnreadReactionCountForPhotoAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _storage.Verify(x => x.GetPublicUrl(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyWidget_WhenActiveGroupDoesNotExist()
    {
        var currentUserId = Guid.NewGuid();
        var activeGroupId = Guid.NewGuid();

        var user = User.Create(
            "widget-missing-group@test.com",
            "hash",
            "widget_missing_group",
            "Widget",
            "Missing");

        user.SetActiveGroup(activeGroupId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _groups.Setup(x => x.GetByIdAsync(activeGroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group?)null);

        var handler = Create();

        var result = await handler.Handle(new GetWidgetQuery(), CancellationToken.None);

        result.ActiveGroupId.Should().BeNull();
        result.ActiveGroupName.Should().BeNull();
        result.LatestPhotoId.Should().BeNull();
        result.LatestPhotoUrl.Should().BeNull();
        result.NewReactionsCount.Should().Be(0);

        _photos.Verify(x => x.GetLatestByGroupAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _notifications.Verify(x => x.GetUnreadReactionCountForPhotoAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _storage.Verify(x => x.GetPublicUrl(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyWidget_WhenUserIsNotMemberOfActiveGroup()
    {
        var currentUserId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        var user = User.Create(
            "widget-outsider@test.com",
            "hash",
            "widget_outsider",
            "Widget",
            "Outsider");

        var group = Group.Create("Secret Group", ownerId);
        user.SetActiveGroup(group.Id);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = Create();

        var result = await handler.Handle(new GetWidgetQuery(), CancellationToken.None);

        result.ActiveGroupId.Should().BeNull();
        result.ActiveGroupName.Should().BeNull();
        result.LatestPhotoId.Should().BeNull();
        result.LatestPhotoUrl.Should().BeNull();
        result.NewReactionsCount.Should().Be(0);

        _photos.Verify(x => x.GetLatestByGroupAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _notifications.Verify(x => x.GetUnreadReactionCountForPhotoAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _storage.Verify(x => x.GetPublicUrl(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnGroupWithoutPhoto_WhenLatestPhotoDoesNotExist()
    {
        var currentUserId = Guid.NewGuid();

        var user = User.Create(
            "widget-no-photo@test.com",
            "hash",
            "widget_no_photo",
            "Widget",
            "NoPhoto");

        var group = Group.Create("My Group", currentUserId);
        group.SetAvatar(currentUserId, "https://cdn.example.com/group-avatar.jpg");
        user.SetActiveGroup(group.Id);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _photos.Setup(x => x.GetLatestByGroupAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Photo?)null);

        var handler = Create();

        var result = await handler.Handle(new GetWidgetQuery(), CancellationToken.None);

        result.ActiveGroupId.Should().Be(group.Id);
        result.ActiveGroupName.Should().Be("My Group");
        result.ActiveGroupAvatarUrl.Should().Be("https://cdn.example.com/group-avatar.jpg");
        result.LatestPhotoId.Should().BeNull();
        result.LatestPhotoUrl.Should().BeNull();
        result.LatestPhotoCreatedAt.Should().BeNull();
        result.NewReactionsCount.Should().Be(0);

        _notifications.Verify(x => x.GetUnreadReactionCountForPhotoAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _storage.Verify(x => x.GetPublicUrl(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnFullWidget_WhenLatestPhotoExists()
    {
        var currentUserId = Guid.NewGuid();

        var user = User.Create(
            "widget-full@test.com",
            "hash",
            "widget_full",
            "Widget",
            "Full");

        var group = Group.Create("Family", currentUserId);
        group.SetAvatar(currentUserId, "https://cdn.example.com/family-avatar.jpg");
        user.SetActiveGroup(group.Id);

        var photo = Photo.Create(
            group.Id,
            currentUserId,
            "photos/family/latest.jpg",
            "image/jpeg",
            1024);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _photos.Setup(x => x.GetLatestByGroupAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);

        _notifications.Setup(x => x.GetUnreadReactionCountForPhotoAsync(
                currentUserId,
                group.Id,
                photo.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        _storage.Setup(x => x.GetPublicUrl(photo.StorageKey))
            .Returns("https://cdn.example.com/photos/family/latest.jpg");

        var handler = Create();

        var result = await handler.Handle(new GetWidgetQuery(), CancellationToken.None);

        result.ActiveGroupId.Should().Be(group.Id);
        result.ActiveGroupName.Should().Be("Family");
        result.ActiveGroupAvatarUrl.Should().Be("https://cdn.example.com/family-avatar.jpg");
        result.LatestPhotoId.Should().Be(photo.Id);
        result.LatestPhotoUrl.Should().Be("https://cdn.example.com/photos/family/latest.jpg");
        result.LatestPhotoCreatedAt.Should().Be(photo.CreatedAt);
        result.NewReactionsCount.Should().Be(3);

        _storage.Verify(x => x.GetPublicUrl(photo.StorageKey), Times.Once);
        _notifications.Verify(x => x.GetUnreadReactionCountForPhotoAsync(
            currentUserId,
            group.Id,
            photo.Id,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}