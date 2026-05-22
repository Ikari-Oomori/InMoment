using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Media.Comments.List;
using InMoment.Domain.Common;
using InMoment.Domain.Media;
using InMoment.Domain.Users;
using Moq;

namespace InMoment.Tests.Application.Tests.Media.Comments.List;

public sealed class ListCommentsHandlerTests
{
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IPhotoRepository> _photos = new();
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<ICommentRepository> _comments = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IBlockedUserRepository> _blocks = new();
    private readonly Mock<ICommentReactionRepository> _commentReactions = new();

    private ListCommentsHandler Create()
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
            new ListCommentsQuery(Guid.Empty, 50),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("PhotoId is required.");
    }

    [Fact]
    public async Task Handle_ShouldReturnComments_WithAuthorAndReplyContext()
    {
        var currentUserId = Guid.NewGuid();
        var photoOwnerId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var replierId = currentUserId;

        var photo = Photo.Create(Guid.NewGuid(), photoOwnerId, "photos/1.jpg", "image/jpeg", 100);

        var root = Comment.CreateRoot(photo.Id, authorId, "root comment text");
        var reply = Comment.CreateReply(photo.Id, replierId, root.Id, "reply text");

        var author = User.Create(
            "author@test.com",
            "hash",
            "author.user",
            "Author",
            "User");

        typeof(User).GetProperty(nameof(User.Id))!.SetValue(author, authorId);

        author.SetProfilePhoto("https://cdn.example.com/author.jpg");

        var replier = User.Create(
            "replier@test.com",
            "hash",
            "replier.user",
            "Reply",
            "User");

        typeof(User).GetProperty(nameof(User.Id))!.SetValue(replier, replierId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _comments.Setup(x => x.GetByPhotoAsync(photo.Id, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { root, reply });
        _comments.Setup(x => x.GetByIdAsync(root.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(root);

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, replierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _users.Setup(x => x.GetByIdsAsync(
                It.Is<IReadOnlyList<Guid>>(ids => ids.Contains(authorId) && ids.Contains(replierId)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { author, replier });
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
            new ListCommentsQuery(photo.Id, 50),
            CancellationToken.None);

        result.Should().HaveCount(2);

        result[0].Id.Should().Be(root.Id);
        result[0].UserId.Should().Be(authorId);
        result[0].UserName.Should().Be("author.user");
        result[0].FirstName.Should().Be("Author");
        result[0].LastName.Should().Be("User");
        result[0].ProfilePhotoUrl.Should().Be("https://cdn.example.com/author.jpg");
        result[0].ParentCommentId.Should().BeNull();
        result[0].ParentCommentTextPreview.Should().BeNull();
        result[0].IsMine.Should().BeFalse();

        result[1].Id.Should().Be(reply.Id);
        result[1].UserId.Should().Be(replierId);
        result[1].UserName.Should().Be("replier.user");
        result[1].ParentCommentId.Should().Be(root.Id);
        result[1].ParentCommentUserId.Should().Be(authorId);
        result[1].ParentCommentUserName.Should().Be("author.user");
        result[1].ParentCommentTextPreview.Should().Be("root comment text");
        result[1].IsMine.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldSkipDeletedAndBlockedComments()
    {
        var currentUserId = Guid.NewGuid();
        var visibleUserId = Guid.NewGuid();
        var blockedUserId = Guid.NewGuid();

        var photo = Photo.Create(Guid.NewGuid(), visibleUserId, "photos/1.jpg", "image/jpeg", 100);

        var visible = Comment.CreateRoot(photo.Id, visibleUserId, "visible");
        var deleted = Comment.CreateRoot(photo.Id, visibleUserId, "deleted");
        deleted.Delete(visibleUserId);
        var blocked = Comment.CreateRoot(photo.Id, blockedUserId, "blocked");

        var visibleUser = User.Create(
            "visible@test.com",
            "hash",
            "visible.user",
            "Visible",
            "User");

        typeof(User).GetProperty(nameof(User.Id))!.SetValue(visibleUser, visibleUserId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(photo.GroupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _comments.Setup(x => x.GetByPhotoAsync(photo.Id, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { visible, deleted, blocked });

        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, visibleUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, blockedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _users.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { visibleUser });

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
            new ListCommentsQuery(photo.Id, 50),
            CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Text.Should().Be("visible");
    }
}