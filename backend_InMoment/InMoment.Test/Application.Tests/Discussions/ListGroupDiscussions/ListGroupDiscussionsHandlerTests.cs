using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Abstractions.Storage;
using InMoment.Application.Features.Discussions.ListGroupDiscussions;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;
using InMoment.Domain.Media;
using InMoment.Domain.Users;
using Moq;

namespace InMoment.Application.Tests.Discussions.ListGroupDiscussions;

public sealed class ListGroupDiscussionsHandlerTests
{
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IPhotoRepository> _photos = new();
    private readonly Mock<ICommentRepository> _comments = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IFileStorage> _storage = new();
    private readonly Mock<IBlockedUserRepository> _blocks = new();
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IReactionRepository> _reactions = new();

    private ListGroupDiscussionsHandler Create()
        => new(
            _groups.Object,
            _photos.Object,
            _comments.Object,
            _users.Object,
            _storage.Object,
            _blocks.Object,
            _current.Object,
            _reactions.Object);

    [Fact]
    public async Task Handle_ShouldThrow_WhenGroupIdEmpty()
    {
        var handler = Create();

        var act = () => handler.Handle(
            new ListGroupDiscussionsQuery(Guid.Empty),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("GroupId is required.");
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenGroupNotFound()
    {
        var groupId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        _current.Setup(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group?)null);

        var handler = Create();

        var act = () => handler.Handle(
            new ListGroupDiscussionsQuery(groupId),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Group not found.");
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserIsNotMember()
    {
        var ownerId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var group = Group.Create("private-group", ownerId);

        _current.Setup(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = Create();

        var act = () => handler.Handle(
            new ListGroupDiscussionsQuery(group.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not a member of this group.");
    }

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenNoPhotos()
    {
        var currentUserId = Guid.NewGuid();
        var group = Group.Create("test-group", currentUserId);

        _current.Setup(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _photos.Setup(x => x.GetFeedByGroupAsync(group.Id, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Photo>());

        var handler = Create();

        var result = await handler.Handle(
            new ListGroupDiscussionsQuery(group.Id),
            CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldUseDefaultLimit_WhenLimitInvalid()
    {
        var currentUserId = Guid.NewGuid();
        var group = Group.Create("test-group", currentUserId);

        _current.Setup(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _photos.Setup(x => x.GetFeedByGroupAsync(group.Id, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Photo>());

        var handler = Create();

        await handler.Handle(
            new ListGroupDiscussionsQuery(group.Id, 999),
            CancellationToken.None);

        _photos.Verify(x => x.GetFeedByGroupAsync(group.Id, 30, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldBuildDiscussionItems()
    {
        var currentUserId = Guid.NewGuid();
        var commenterId = Guid.NewGuid();
        var group = Group.Create("test-group", currentUserId);

        var photo = Photo.Create(
            group.Id,
            currentUserId,
            "groups/test/photos/photo-1.jpg",
            "image/jpeg",
            1234,
            "caption text");

        var latestComment = Comment.CreateRoot(photo.Id, commenterId, "latest comment text");
        var author = CreateUser(currentUserId, "author_user");
        author.SetProfilePhoto("https://cdn.example.com/author.jpg");

        var commenter = CreateUser(commenterId, "commenter_user");
        commenter.SetProfilePhoto("https://cdn.example.com/commenter.jpg");

        _current.Setup(x => x.UserId).Returns(currentUserId);

        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _photos.Setup(x => x.GetFeedByGroupAsync(group.Id, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { photo });

        _comments.Setup(x => x.GetCountsByPhotoIdsAsync(
                It.Is<IReadOnlyList<Guid>>(ids => ids.Count == 1 && ids[0] == photo.Id),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int>
            {
                [photo.Id] = 4
            });

        _comments.Setup(x => x.GetLatestByPhotoIdsAsync(
                It.Is<IReadOnlyList<Guid>>(ids => ids.Count == 1 && ids[0] == photo.Id),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, Comment>
            {
                [photo.Id] = latestComment
            });

        _users.Setup(x => x.GetByIdsAsync(
                It.Is<IReadOnlyList<Guid>>(ids =>
                    ids.Count == 2 &&
                    ids.Contains(currentUserId) &&
                    ids.Contains(commenterId)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { author, commenter });

        _storage.Setup(x => x.GetPublicUrl(photo.StorageKey))
            .Returns("https://cdn.example.com/photo-1.jpg");

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, commenterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _reactions.Setup(x => x.GetSummariesByPhotoIdsAsync(
        It.Is<IReadOnlyList<Guid>>(ids => ids.Count == 1 && ids[0] == photo.Id),
        It.IsAny<CancellationToken>()))
    .ReturnsAsync(new Dictionary<Guid, IReadOnlyDictionary<ReactionType, int>>
    {
        [photo.Id] = new Dictionary<ReactionType, int>
        {
            [ReactionType.Heart] = 2,
            [ReactionType.Wow] = 1,
        }
    });

        _reactions.Setup(x => x.GetUserReactionsByPhotoIdsAsync(
                It.Is<IReadOnlyList<Guid>>(ids => ids.Count == 1 && ids[0] == photo.Id),
                currentUserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ReactionType>
            {
                [photo.Id] = ReactionType.Heart
            });

        var handler = Create();

        var result = await handler.Handle(
            new ListGroupDiscussionsQuery(group.Id),
            CancellationToken.None);

        result.Should().HaveCount(1);

        var item = result[0];
        item.PhotoId.Should().Be(photo.Id);
        item.PhotoUrl.Should().Be("https://cdn.example.com/photo-1.jpg");
        item.PhotoCreatedAt.Should().Be(photo.CreatedAt);
        item.PhotoCaption.Should().Be("caption text");
        item.PhotoAuthorUserId.Should().Be(currentUserId);
        item.PhotoAuthorUserName.Should().Be("author_user");
        item.PhotoAuthorProfilePhotoUrl.Should().Be("https://cdn.example.com/author.jpg");
        item.CommentsCount.Should().Be(4);
        item.LatestCommentText.Should().Be("latest comment text");
        item.LatestCommentUserId.Should().Be(commenterId);
        item.LatestCommentUserName.Should().Be("commenter_user");
        item.LatestCommentUserProfilePhotoUrl.Should().Be("https://cdn.example.com/commenter.jpg");
        item.LatestCommentCreatedAt.Should().Be(latestComment.CreatedAt);
        item.LastActivityAt.Should().Be(latestComment.CreatedAt);
        item.ReactionsCount.Should().Be(3);
        item.MyReaction.Should().Be(ReactionType.Heart);
        item.Reactions.Should().HaveCount(2);
        item.Reactions.Should().ContainSingle(x => x.Type == ReactionType.Heart && x.Count == 2);
        item.Reactions.Should().ContainSingle(x => x.Type == ReactionType.Wow && x.Count == 1);
    }


    [Fact]
    public async Task Handle_ShouldHideLatestComment_WhenCommentAuthorBlocked()
    {
        var currentUserId = Guid.NewGuid();
        var blockedUserId = Guid.NewGuid();
        var group = Group.Create("test-group", currentUserId);

        var photo = Photo.Create(
            group.Id,
            currentUserId,
            "groups/test/photos/photo-1.jpg",
            "image/jpeg",
            1234,
            "caption text");

        var latestComment = Comment.CreateRoot(photo.Id, blockedUserId, "blocked text");
        var author = CreateUser(currentUserId, "owner_user");
        author.SetProfilePhoto("https://cdn.example.com/owner.jpg");

        _current.Setup(x => x.UserId).Returns(currentUserId);

        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _photos.Setup(x => x.GetFeedByGroupAsync(group.Id, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { photo });

        _comments.Setup(x => x.GetCountsByPhotoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int>
            {
                [photo.Id] = 2
            });

        _comments.Setup(x => x.GetLatestByPhotoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, Comment>
            {
                [photo.Id] = latestComment
            });

        _users.Setup(x => x.GetByIdsAsync(
                It.Is<IReadOnlyList<Guid>>(ids => ids.Contains(currentUserId) && ids.Contains(blockedUserId)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { author, CreateUser(blockedUserId, "blocked_user") });

        _storage.Setup(x => x.GetPublicUrl(photo.StorageKey))
            .Returns("https://cdn.example.com/photo-1.jpg");

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, blockedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _reactions.Setup(x => x.GetSummariesByPhotoIdsAsync(
        It.Is<IReadOnlyList<Guid>>(ids => ids.Count == 1 && ids[0] == photo.Id),
        It.IsAny<CancellationToken>()))
    .ReturnsAsync(new Dictionary<Guid, IReadOnlyDictionary<ReactionType, int>>
    {
        [photo.Id] = new Dictionary<ReactionType, int>
        {
            [ReactionType.Heart] = 1,
        }
    });

        _reactions.Setup(x => x.GetUserReactionsByPhotoIdsAsync(
                It.Is<IReadOnlyList<Guid>>(ids => ids.Count == 1 && ids[0] == photo.Id),
                currentUserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ReactionType>
            {
                [photo.Id] = ReactionType.Heart
            });

        var handler = Create();

        var result = await handler.Handle(
            new ListGroupDiscussionsQuery(group.Id),
            CancellationToken.None);

        result.Should().HaveCount(1);

        var item = result[0];
        item.PhotoAuthorUserId.Should().Be(currentUserId);
        item.PhotoAuthorUserName.Should().Be("owner_user");
        item.PhotoAuthorProfilePhotoUrl.Should().Be("https://cdn.example.com/owner.jpg");
        item.PhotoCaption.Should().Be("caption text");
        item.CommentsCount.Should().Be(2);
        item.LatestCommentText.Should().BeNull();
        item.LatestCommentUserId.Should().BeNull();
        item.LatestCommentUserName.Should().BeNull();
        item.LatestCommentUserProfilePhotoUrl.Should().BeNull();
        item.LatestCommentCreatedAt.Should().BeNull();
        item.LastActivityAt.Should().Be(photo.CreatedAt);
        item.ReactionsCount.Should().Be(1);
        item.MyReaction.Should().Be(ReactionType.Heart);
        item.Reactions.Should().ContainSingle(x => x.Type == ReactionType.Heart && x.Count == 1);
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