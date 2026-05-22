using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Abstractions.Storage;
using InMoment.Application.Features.Media.GetGroupFeed;
using InMoment.Domain.Common;
using InMoment.Domain.Media;
using Moq;

namespace InMoment.Application.Tests.Media.GetGroupFeed;

public sealed class GetGroupFeedPageHandlerTests
{
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IPhotoRepository> _photos = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IFileStorage> _storage = new();
    private readonly Mock<IReactionRepository> _reactions = new();
    private readonly Mock<ICommentRepository> _comments = new();

    private GetGroupFeedPageHandler Create()
        => new(
            _current.Object,
            _groups.Object,
            _photos.Object,
            _users.Object,
            _storage.Object,
            _reactions.Object,
            _comments.Object);

    [Fact]
    public async Task Handle_ShouldThrow_WhenGroupIdEmpty()
    {
        var handler = Create();

        var act = () => handler.Handle(
            new GetGroupFeedPageQuery(Guid.Empty),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("GroupId is required.");
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserNotMember()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        _current.Setup(x => x.UserId).Returns(userId);
        _groups.Setup(x => x.IsMemberAsync(groupId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = Create();

        var act = () => handler.Handle(
            new GetGroupFeedPageQuery(groupId),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not an active member of this group.");
    }

    [Fact]
    public async Task Handle_ShouldUseDefaultLimit_WhenInvalid()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        _current.Setup(x => x.UserId).Returns(userId);
        _groups.Setup(x => x.IsMemberAsync(groupId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _photos.Setup(x => x.GetPageByGroupAsync(
                groupId,
                20,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Photo>());

        var handler = Create();

        await handler.Handle(
            new GetGroupFeedPageQuery(groupId, 999, null),
            CancellationToken.None);

        _photos.Verify(x => x.GetPageByGroupAsync(
            groupId,
            20,
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCursorInvalid()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        _current.Setup(x => x.UserId).Returns(userId);
        _groups.Setup(x => x.IsMemberAsync(groupId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = Create();

        var act = () => handler.Handle(
            new GetGroupFeedPageQuery(groupId, 20, "bad-cursor"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Invalid cursor format.");
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyPage_WhenNoPhotos()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        _current.Setup(x => x.UserId).Returns(userId);
        _groups.Setup(x => x.IsMemberAsync(groupId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _photos.Setup(x => x.GetPageByGroupAsync(
                groupId,
                20,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Photo>());

        var handler = Create();

        var result = await handler.Handle(
            new GetGroupFeedPageQuery(groupId, 20, null),
            CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldParseCursor_AndPassToRepository()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var dt = new DateTime(2026, 4, 1, 12, 30, 0, DateTimeKind.Utc);
        var photoId = Guid.NewGuid();
        var cursor = $"{dt:O}|{photoId:D}";

        _current.Setup(x => x.UserId).Returns(userId);
        _groups.Setup(x => x.IsMemberAsync(groupId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _photos.Setup(x => x.GetPageByGroupAsync(
                groupId,
                20,
                dt,
                photoId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Photo>());

        var handler = Create();

        await handler.Handle(
            new GetGroupFeedPageQuery(groupId, 20, cursor),
            CancellationToken.None);

        _photos.Verify(x => x.GetPageByGroupAsync(
            groupId,
            20,
            dt,
            photoId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldBuildItemsCorrectly()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var photo = Photo.Create(groupId, Guid.NewGuid(), "key", "image/jpeg", 100);

        _current.Setup(x => x.UserId).Returns(userId);
        _groups.Setup(x => x.IsMemberAsync(groupId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _photos.Setup(x => x.GetPageByGroupAsync(
               groupId,
               20,
               null,
               null,
               It.IsAny<CancellationToken>()))
           .ReturnsAsync(new[] { photo });

        _users.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<InMoment.Domain.Users.User>());

        _storage.Setup(x => x.GetPublicUrl(photo.StorageKey))
            .Returns("url");

        _reactions.Setup(x => x.GetSummariesByPhotoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyDictionary<ReactionType, int>>
            {
                [photo.Id] = new Dictionary<ReactionType, int>
                {
                    [ReactionType.Heart] = 2,
                    [ReactionType.Wow] = 1
                }
            });

        _reactions.Setup(x => x.GetUserReactionsByPhotoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), userId, It.IsAny<CancellationToken>()))
           .ReturnsAsync(new Dictionary<Guid, ReactionType>
           {
               [photo.Id] = ReactionType.Heart
           });

        _comments.Setup(x => x.GetCountsByPhotoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int>
            {
                [photo.Id] = 7
            });

        var handler = Create();

        var result = await handler.Handle(
            new GetGroupFeedPageQuery(groupId),
            CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.NextCursor.Should().BeNull();

        var item = result.Items[0];
        item.PhotoId.Should().Be(photo.Id);
        item.GroupId.Should().Be(groupId);
        item.AuthorId.Should().Be(photo.UploadedByUserId);
        item.AuthorUserName.Should().BeEmpty();
        item.AuthorProfilePhotoUrl.Should().BeNull();
        item.Url.Should().Be("url");
        item.Caption.Should().BeNull();
        item.ContentType.Should().Be("image/jpeg");
        item.SizeBytes.Should().Be(100);
        item.MyReaction.Should().Be(ReactionType.Heart);
        item.CommentsCount.Should().Be(7);
        item.Reactions.Should().Contain(x => x.Type == ReactionType.Heart && x.Count == 2);
        item.Reactions.Should().Contain(x => x.Type == ReactionType.Wow && x.Count == 1);
    }

    [Fact]
    public async Task Handle_ShouldGenerateNextCursor_WhenItemsCountEqualsLimit()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var photo1 = Photo.Create(groupId, Guid.NewGuid(), "1", "image/jpeg", 100);
        var photo2 = Photo.Create(groupId, Guid.NewGuid(), "2", "image/jpeg", 100);

        SetCreatedAt(photo1, new DateTime(2026, 4, 10, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(photo2, new DateTime(2026, 4, 9, 10, 0, 0, DateTimeKind.Utc));

        _current.Setup(x => x.UserId).Returns(userId);
        _groups.Setup(x => x.IsMemberAsync(groupId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _photos.Setup(x => x.GetPageByGroupAsync(
                groupId,
                2,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { photo1, photo2 });

        _users.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<InMoment.Domain.Users.User>());

        _storage.Setup(x => x.GetPublicUrl(It.IsAny<string>()))
            .Returns<string>(x => $"url:{x}");

        _reactions.Setup(x => x.GetSummariesByPhotoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyDictionary<ReactionType, int>>());

        _reactions.Setup(x => x.GetUserReactionsByPhotoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ReactionType>());

        _comments.Setup(x => x.GetCountsByPhotoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int>());

        var handler = Create();

        var result = await handler.Handle(
            new GetGroupFeedPageQuery(groupId, 2, null),
            CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.NextCursor.Should().NotBeNull();
        result.NextCursor.Should().Be($"{photo2.CreatedAt.ToUniversalTime():O}|{photo2.Id:D}");
    }

    [Fact]
    public async Task Handle_ShouldNotGenerateNextCursor_WhenItemsCountLessThanLimit()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var photo = Photo.Create(groupId, Guid.NewGuid(), "1", "image/jpeg", 100);
        SetCreatedAt(photo, new DateTime(2026, 4, 10, 10, 0, 0, DateTimeKind.Utc));

        _current.Setup(x => x.UserId).Returns(userId);
        _groups.Setup(x => x.IsMemberAsync(groupId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _photos.Setup(x => x.GetPageByGroupAsync(
                groupId,
                2,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { photo });

        _users.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Array.Empty<InMoment.Domain.Users.User>());

        _storage.Setup(x => x.GetPublicUrl(It.IsAny<string>()))
            .Returns<string>(x => $"url:{x}");

        _reactions.Setup(x => x.GetSummariesByPhotoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyDictionary<ReactionType, int>>());

        _reactions.Setup(x => x.GetUserReactionsByPhotoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ReactionType>());

        _comments.Setup(x => x.GetCountsByPhotoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int>());

        var handler = Create();

        var result = await handler.Handle(
            new GetGroupFeedPageQuery(groupId, 2, null),
            CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.NextCursor.Should().BeNull();
    }

    private static void SetCreatedAt(Photo photo, DateTime createdAtUtc)
    {
        typeof(Photo)
            .GetProperty(nameof(Photo.CreatedAt))!
            .SetValue(photo, createdAtUtc);
    }
}