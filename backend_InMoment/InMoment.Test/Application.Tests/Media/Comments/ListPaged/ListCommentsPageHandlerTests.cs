using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Media.Comments.ListPaged;
using InMoment.Domain.Common;
using InMoment.Domain.Media;
using InMoment.Domain.Users;
using Moq;

namespace InMoment.Tests.Application.Tests.Media.Comments.ListPaged;

public sealed class ListCommentsPageHandlerTests
{
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IPhotoRepository> _photos = new();
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<ICommentRepository> _comments = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IBlockedUserRepository> _blocks = new();
    private readonly Mock<ICommentReactionRepository> _commentReactions = new();

    private ListCommentsPageHandler Create()
    => new(
        _current.Object,
        _photos.Object,
        _groups.Object,
        _comments.Object,
        _users.Object,
        _blocks.Object,
        _commentReactions.Object);

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenPhotoIdEmpty()
    {
        var handler = Create();

        var act = () => handler.Handle(
            new ListCommentsPageQuery(Guid.Empty, 20, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("PhotoId is required.");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenPhotoNotFound()
    {
        var photoId = Guid.NewGuid();

        _photos.Setup(x => x.GetByIdAsync(photoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Photo?)null);

        var handler = Create();

        var act = () => handler.Handle(
            new ListCommentsPageQuery(photoId, 20, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Photo not found.");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenPhotoDeleted()
    {
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var photo = Photo.Create(groupId, userId, "photos/1.jpg", "image/jpeg", 100);
        photo.MarkDeleted(userId, userId);

        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);

        var handler = Create();

        var act = () => handler.Handle(
            new ListCommentsPageQuery(photo.Id, 20, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Photo not found.");
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUserNotGroupMember()
    {
        var currentUserId = Guid.NewGuid();
        var photo = Photo.Create(Guid.NewGuid(), Guid.NewGuid(), "photos/1.jpg", "image/jpeg", 100);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = Create();

        var act = () => handler.Handle(
            new ListCommentsPageQuery(photo.Id, 20, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not an active member of this group.");
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenCursorInvalid()
    {
        var currentUserId = Guid.NewGuid();
        var photo = Photo.Create(Guid.NewGuid(), Guid.NewGuid(), "photos/1.jpg", "image/jpeg", 100);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = Create();

        var act = () => handler.Handle(
            new ListCommentsPageQuery(photo.Id, 20, "bad-cursor"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Invalid cursor format.");
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyPage_WhenRepositoryReturnsNoItems()
    {
        var currentUserId = Guid.NewGuid();
        var photo = Photo.Create(Guid.NewGuid(), Guid.NewGuid(), "photos/1.jpg", "image/jpeg", 100);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _comments.Setup(x => x.GetPageByPhotoAsync(
                photo.Id,
                40,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Comment>());

        var handler = Create();

        var result = await handler.Handle(
            new ListCommentsPageQuery(photo.Id, 20, null),
            CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldNormalizeLimit_WhenOutOfRange()
    {
        var currentUserId = Guid.NewGuid();
        var photo = Photo.Create(Guid.NewGuid(), Guid.NewGuid(), "photos/1.jpg", "image/jpeg", 100);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _comments.Setup(x => x.GetPageByPhotoAsync(
                photo.Id,
                40,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Comment>());

        var handler = Create();

        await handler.Handle(
            new ListCommentsPageQuery(photo.Id, 999, null),
            CancellationToken.None);

        _comments.Verify(x => x.GetPageByPhotoAsync(
            photo.Id,
            40,
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldParseCursor_AndPassItToRepository()
    {
        var currentUserId = Guid.NewGuid();
        var photo = Photo.Create(Guid.NewGuid(), Guid.NewGuid(), "photos/1.jpg", "image/jpeg", 100);
        var createdAt = new DateTime(2026, 4, 3, 10, 0, 0, DateTimeKind.Utc);
        var commentId = Guid.NewGuid();
        var cursor = $"{createdAt:O}|{commentId:D}";

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _comments.Setup(x => x.GetPageByPhotoAsync(
                photo.Id,
                40,
                It.Is<DateTime?>(d => d == createdAt),
                It.Is<Guid?>(g => g == commentId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Comment>());

        var handler = Create();

        await handler.Handle(
            new ListCommentsPageQuery(photo.Id, 20, cursor),
            CancellationToken.None);

        _comments.VerifyAll();
    }

    [Fact]
    public async Task Handle_ShouldFilterDeletedAndBlockedComments_AndBuildNextCursor_WithAuthorData()
    {
        var currentUserId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var blockedUserId = Guid.NewGuid();

        var photo = Photo.Create(Guid.NewGuid(), authorId, "photos/1.jpg", "image/jpeg", 100);

        var visibleFirst = Comment.CreateRoot(photo.Id, authorId, "first");
        await Task.Delay(5);
        var deleted = Comment.CreateRoot(photo.Id, authorId, "deleted");
        deleted.Delete(authorId);
        await Task.Delay(5);
        var blocked = Comment.CreateRoot(photo.Id, blockedUserId, "blocked");
        await Task.Delay(5);
        var visibleSecond = Comment.CreateRoot(photo.Id, currentUserId, "second");

        var author = User.Create(
            "author@test.com",
            "hash",
            "author.user",
            "Author",
            "User");
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(author, authorId);

        var currentUser = User.Create(
            "me@test.com",
            "hash",
            "me.user",
            "Me",
            "User");
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(currentUser, currentUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _comments.Setup(x => x.GetPageByPhotoAsync(
                photo.Id,
                4,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { visibleFirst, deleted, blocked, visibleSecond });

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, visibleFirst.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, blockedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, visibleSecond.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _users.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { author, currentUser });

        _commentReactions
            .Setup(x => x.GetSummariesByCommentIdsAsync(
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyDictionary<ReactionType, int>>());

        _commentReactions
            .Setup(x => x.GetUserReactionsByCommentIdsAsync(
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ReactionType>());

        var handler = Create();

        var result = await handler.Handle(
            new ListCommentsPageQuery(photo.Id, 2, null),
            CancellationToken.None);

        result.Items.Should().HaveCount(2);

        result.Items[0].Id.Should().Be(visibleFirst.Id);
        result.Items[0].Text.Should().Be("first");
        result.Items[0].UserName.Should().Be("author.user");
        result.Items[0].IsMine.Should().BeFalse();

        result.Items[1].Id.Should().Be(visibleSecond.Id);
        result.Items[1].Text.Should().Be("second");
        result.Items[1].UserName.Should().Be("me.user");
        result.Items[1].IsMine.Should().BeTrue();

        result.NextCursor.Should().Be($"{visibleSecond.CreatedAt.ToUniversalTime():O}|{visibleSecond.Id:D}");
    }

    [Fact]
    public async Task Handle_ShouldReturnReplyContext_WhenParentVisible()
    {
        var currentUserId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var photo = Photo.Create(Guid.NewGuid(), authorId, "photos/1.jpg", "image/jpeg", 100);

        var root = Comment.CreateRoot(photo.Id, authorId, "parent text");
        var reply = Comment.CreateReply(photo.Id, currentUserId, root.Id, "reply text");

        var author = User.Create(
            "author2@test.com",
            "hash",
            "author2.user",
            "Author2",
            "User2");
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(author, authorId);

        var currentUser = User.Create(
            "me2@test.com",
            "hash",
            "me2.user",
            "Me2",
            "User2");
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(currentUser, currentUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _comments.Setup(x => x.GetPageByPhotoAsync(
                photo.Id,
                40,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { root, reply });

        _comments.Setup(x => x.GetByIdAsync(root.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(root);

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _users.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { author, currentUser });

        _commentReactions
            .Setup(x => x.GetSummariesByCommentIdsAsync(
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyDictionary<ReactionType, int>>());

        _commentReactions
            .Setup(x => x.GetUserReactionsByCommentIdsAsync(
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ReactionType>());

        var handler = Create();

        var result = await handler.Handle(
            new ListCommentsPageQuery(photo.Id, 20, null),
            CancellationToken.None);

        result.Items.Should().HaveCount(2);

        result.Items[1].ParentCommentId.Should().Be(root.Id);
        result.Items[1].ParentCommentUserId.Should().Be(authorId);
        result.Items[1].ParentCommentUserName.Should().Be("author2.user");
        result.Items[1].ParentCommentTextPreview.Should().Be("parent text");
    }

    [Fact]
    public async Task Handle_ShouldReturnNullNextCursor_WhenRawItemsCountLessThanLimit()
    {
        var currentUserId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var photo = Photo.Create(Guid.NewGuid(), authorId, "photos/1.jpg", "image/jpeg", 100);
        var visible = Comment.CreateRoot(photo.Id, authorId, "first");

        var author = User.Create(
            "author3@test.com",
            "hash",
            "author3.user",
            "Author3",
            "User3");
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(author, authorId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _comments.Setup(x => x.GetPageByPhotoAsync(
                photo.Id,
                4,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { visible });

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, visible.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _users.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { author });

        _commentReactions
            .Setup(x => x.GetSummariesByCommentIdsAsync(
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyDictionary<ReactionType, int>>());

        _commentReactions
            .Setup(x => x.GetUserReactionsByCommentIdsAsync(
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ReactionType>());

        var handler = Create();

        var result = await handler.Handle(
            new ListCommentsPageQuery(photo.Id, 2, null),
            CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenAllRawItemsInvisible()
    {
        var currentUserId = Guid.NewGuid();
        var blockedUserId = Guid.NewGuid();

        var photo = Photo.Create(Guid.NewGuid(), blockedUserId, "photos/1.jpg", "image/jpeg", 100);

        var deleted = Comment.CreateRoot(photo.Id, blockedUserId, "deleted");
        deleted.Delete(blockedUserId);

        var blocked = Comment.CreateRoot(photo.Id, blockedUserId, "blocked");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _comments.Setup(x => x.GetPageByPhotoAsync(
                photo.Id,
                40,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { deleted, blocked });

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, blockedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _commentReactions
            .Setup(x => x.GetSummariesByCommentIdsAsync(
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyDictionary<ReactionType, int>>());

        _commentReactions
            .Setup(x => x.GetUserReactionsByCommentIdsAsync(
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ReactionType>());

        var handler = Create();

        var result = await handler.Handle(
            new ListCommentsPageQuery(photo.Id, 20, null),
            CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.NextCursor.Should().BeNull();
    }
}