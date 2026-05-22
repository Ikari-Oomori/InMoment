using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Abstractions.Storage;
using InMoment.Application.Features.Media.Saved.ListSavedPhotos;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;
using InMoment.Domain.Media;
using InMoment.Domain.Users;
using Moq;

namespace InMoment.Application.Tests.Media.Saved.ListSavedPhotos;

public sealed class ListSavedPhotosHandlerTests
{
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<ISavedPhotoRepository> _saved = new();
    private readonly Mock<IPhotoRepository> _photos = new();
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IBlockedUserRepository> _blocks = new();
    private readonly Mock<IFileStorage> _storage = new();

    private ListSavedPhotosHandler Create()
        => new(
            _current.Object,
            _saved.Object,
            _photos.Object,
            _groups.Object,
            _users.Object,
            _blocks.Object,
            _storage.Object);

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUnauthorized()
    {
        _current.Setup(x => x.UserId).Returns(Guid.Empty);

        var handler = Create();

        var act = () => handler.Handle(new ListSavedPhotosQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Пользователь не авторизован.");
    }

    [Fact]
    public async Task Handle_ShouldUseDefaultLimit_WhenInvalid()
    {
        var currentUserId = Guid.NewGuid();

        _current.Setup(x => x.UserId).Returns(currentUserId);
        _saved.Setup(x => x.GetPageByUserAsync(currentUserId, 40, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SavedPhoto>());

        var handler = Create();

        await handler.Handle(new ListSavedPhotosQuery(999, null), CancellationToken.None);

        _saved.Verify(x => x.GetPageByUserAsync(currentUserId, 40, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenCursorInvalid()
    {
        var currentUserId = Guid.NewGuid();

        _current.Setup(x => x.UserId).Returns(currentUserId);

        var handler = Create();

        var act = () => handler.Handle(new ListSavedPhotosQuery(20, "bad-cursor"), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Invalid cursor format.");
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyPage_WhenNothingSaved()
    {
        var currentUserId = Guid.NewGuid();

        _current.Setup(x => x.UserId).Returns(currentUserId);
        _saved.Setup(x => x.GetPageByUserAsync(currentUserId, 40, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SavedPhoto>());

        var handler = Create();

        var result = await handler.Handle(new ListSavedPhotosQuery(20, null), CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldFilterInvisibleEntries_AndReturnEnrichedDto()
    {
        var currentUserId = Guid.NewGuid();
        var visibleGroupId = Guid.NewGuid();

        var missingSaved = SavedPhoto.Create(Guid.NewGuid(), currentUserId);

        var deletedSaved = SavedPhoto.Create(Guid.NewGuid(), currentUserId);
        var deletedPhoto = Photo.Create(Guid.NewGuid(), Guid.NewGuid(), "deleted-key", "image/jpeg", 100);
        SetSavedPhotoId(deletedSaved, deletedPhoto.Id);
        deletedPhoto.MarkDeleted(deletedPhoto.UploadedByUserId, deletedPhoto.UploadedByUserId);

        var nonMemberSaved = SavedPhoto.Create(Guid.NewGuid(), currentUserId);
        var nonMemberPhoto = Photo.Create(Guid.NewGuid(), Guid.NewGuid(), "non-member-key", "image/jpeg", 100);
        SetSavedPhotoId(nonMemberSaved, nonMemberPhoto.Id);

        var blockedSaved = SavedPhoto.Create(Guid.NewGuid(), currentUserId);
        var blockedPhoto = Photo.Create(Guid.NewGuid(), Guid.NewGuid(), "blocked-key", "image/jpeg", 100);
        SetSavedPhotoId(blockedSaved, blockedPhoto.Id);

        var visibleSaved = SavedPhoto.Create(Guid.NewGuid(), currentUserId);
        var visiblePhoto = Photo.Create(
            visibleGroupId,
            Guid.NewGuid(),
            "visible-key",
            "image/jpeg",
            100,
            "visible caption");
        SetSavedPhotoId(visibleSaved, visiblePhoto.Id);

        var rawItems = new[] { missingSaved, deletedSaved, nonMemberSaved, blockedSaved, visibleSaved };

        var visibleAuthor = CreateUser(visiblePhoto.UploadedByUserId, "visible_author");
        visibleAuthor.SetProfilePhoto("https://cdn.example.com/u/visible.jpg");

        var visibleGroup = Group.Create("Visible Group", currentUserId);
        typeof(Group).GetProperty(nameof(Group.Id))!.SetValue(visibleGroup, visibleGroupId);
        visibleGroup.SetAvatar(currentUserId, "https://cdn.example.com/g/visible.jpg");

        _current.Setup(x => x.UserId).Returns(currentUserId);

        _saved.Setup(x => x.GetPageByUserAsync(currentUserId, 40, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rawItems);

        _photos.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, Photo>
            {
                [deletedPhoto.Id] = deletedPhoto,
                [nonMemberPhoto.Id] = nonMemberPhoto,
                [blockedPhoto.Id] = blockedPhoto,
                [visiblePhoto.Id] = visiblePhoto
            });

        _groups.Setup(x => x.IsMemberAsync(nonMemberPhoto.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _groups.Setup(x => x.IsMemberAsync(blockedPhoto.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _groups.Setup(x => x.IsMemberAsync(visiblePhoto.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _groups.Setup(x => x.GetByIdAsync(visibleGroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(visibleGroup);

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, blockedPhoto.UploadedByUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, visiblePhoto.UploadedByUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _users.Setup(x => x.GetByIdsAsync(
                It.Is<IReadOnlyList<Guid>>(ids => ids.Count == 1 && ids[0] == visiblePhoto.UploadedByUserId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { visibleAuthor });

        _storage.Setup(x => x.GetPublicUrl(visiblePhoto.StorageKey))
            .Returns("https://cdn.example.com/photos/visible.jpg");

        var handler = Create();

        var result = await handler.Handle(new ListSavedPhotosQuery(20, null), CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.NextCursor.Should().BeNull();

        var item = result.Items[0];
        item.PhotoId.Should().Be(visiblePhoto.Id);
        item.GroupId.Should().Be(visibleGroupId);
        item.GroupName.Should().Be("Visible Group");
        item.GroupAvatarUrl.Should().Be("https://cdn.example.com/g/visible.jpg");
        item.UploadedByUserId.Should().Be(visiblePhoto.UploadedByUserId);
        item.UploadedByUserName.Should().Be("visible_author");
        item.UploadedByUserProfilePhotoUrl.Should().Be("https://cdn.example.com/u/visible.jpg");
        item.IsMine.Should().BeFalse();
        item.PhotoUrl.Should().Be("https://cdn.example.com/photos/visible.jpg");
        item.Caption.Should().Be("visible caption");
    }

    [Fact]
    public async Task Handle_ShouldBuildNextCursor_WhenPageFilled()
    {
        var currentUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var firstSaved = SavedPhoto.Create(Guid.NewGuid(), currentUserId);
        var secondSaved = SavedPhoto.Create(Guid.NewGuid(), currentUserId);

        var firstPhoto = Photo.Create(groupId, currentUserId, "first-key", "image/jpeg", 100, "first");
        var secondPhoto = Photo.Create(groupId, currentUserId, "second-key", "image/jpeg", 100, "second");

        SetSavedPhotoId(firstSaved, firstPhoto.Id);
        SetSavedPhotoId(secondSaved, secondPhoto.Id);

        var group = Group.Create("My Group", currentUserId);
        typeof(Group).GetProperty(nameof(Group.Id))!.SetValue(group, groupId);

        var currentUser = CreateUser(currentUserId, "me_user");

        _current.Setup(x => x.UserId).Returns(currentUserId);

        _saved.Setup(x => x.GetPageByUserAsync(currentUserId, 4, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { firstSaved, secondSaved });

        _photos.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, Photo>
            {
                [firstPhoto.Id] = firstPhoto,
                [secondPhoto.Id] = secondPhoto
            });

        _groups.Setup(x => x.IsMemberAsync(groupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _groups.Setup(x => x.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _users.Setup(x => x.GetByIdsAsync(
                It.Is<IReadOnlyList<Guid>>(ids => ids.Count == 1 && ids[0] == currentUserId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { currentUser });

        _storage.Setup(x => x.GetPublicUrl("first-key")).Returns("https://cdn.example.com/photos/first.jpg");
        _storage.Setup(x => x.GetPublicUrl("second-key")).Returns("https://cdn.example.com/photos/second.jpg");

        var handler = Create();

        var result = await handler.Handle(new ListSavedPhotosQuery(2, null), CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.NextCursor.Should().Be($"{secondSaved.CreatedAt.ToUniversalTime():O}|{secondSaved.Id:D}");
        result.Items[0].IsMine.Should().BeTrue();
        result.Items[1].IsMine.Should().BeTrue();
    }

    private static void SetSavedPhotoId(SavedPhoto savedPhoto, Guid photoId)
    {
        typeof(SavedPhoto)
            .GetProperty(nameof(SavedPhoto.PhotoId))!
            .SetValue(savedPhoto, photoId);
    }

    private static User CreateUser(Guid id, string userName)
    {
        var user = User.Create(
            email: $"{userName}@test.com",
            passwordHash: "hash",
            userName: userName,
            firstName: "Test",
            lastName: "User");

        typeof(User)
            .GetProperty(nameof(User.Id))!
            .SetValue(user, id);

        return user;
    }
}