using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Abstractions.Storage;
using InMoment.Application.Features.Media.GetGroupFeed;
using InMoment.Domain.Common;
using InMoment.Domain.Media;
using Moq;

namespace InMoment.Application.Tests.Media.GetGroupFeed;

public sealed class GetGroupFeedHandlerTests
{
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IPhotoRepository> _photos = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IFileStorage> _storage = new();
    private readonly Mock<IReactionRepository> _reactions = new();
    private readonly Mock<ICommentRepository> _comments = new();

    private GetGroupFeedHandler Create()
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
            new GetGroupFeedQuery(Guid.Empty),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
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
            new GetGroupFeedQuery(groupId),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenNoPhotos()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        _current.Setup(x => x.UserId).Returns(userId);
        _groups.Setup(x => x.IsMemberAsync(groupId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _photos.Setup(x => x.GetFeedByGroupAsync(groupId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Photo>());

        var handler = Create();

        var result = await handler.Handle(
            new GetGroupFeedQuery(groupId),
            CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldBuildFeedItemsCorrectly()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var photo = Photo.Create(groupId, userId, "key", "image/jpeg", 100);

        _current.Setup(x => x.UserId).Returns(userId);

        _groups.Setup(x => x.IsMemberAsync(groupId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _photos.Setup(x => x.GetFeedByGroupAsync(groupId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
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
                    [ReactionType.Heart] = 3,
                    [ReactionType.Wow] = 1
                }
            });

        _reactions.Setup(x => x.GetUserReactionsByPhotoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ReactionType>
            {
                [photo.Id] = ReactionType.Wow
            });

        _comments.Setup(x => x.GetCountsByPhotoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int>
            {
                [photo.Id] = 5
            });

        var handler = Create();

        var result = await handler.Handle(
            new GetGroupFeedQuery(groupId),
            CancellationToken.None);

        result.Should().HaveCount(1);

        var item = result[0];

        item.PhotoId.Should().Be(photo.Id);
        item.AuthorUserName.Should().BeEmpty();
        item.AuthorProfilePhotoUrl.Should().BeNull();
        item.Url.Should().Be("url");
        item.Caption.Should().BeNull();
        item.MyReaction.Should().Be(ReactionType.Wow);
        item.CommentsCount.Should().Be(5);

        item.Reactions.Should().Contain(x => x.Type == ReactionType.Heart && x.Count == 3);
        item.Reactions.Should().Contain(x => x.Type == ReactionType.Wow && x.Count == 1);
    }


    [Fact]
    public async Task Handle_ShouldUseDefaultLimit_WhenInvalid()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        _current.Setup(x => x.UserId).Returns(userId);
        _groups.Setup(x => x.IsMemberAsync(groupId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _photos.Setup(x => x.GetFeedByGroupAsync(groupId, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Photo>());

        var handler = Create();

        await handler.Handle(
            new GetGroupFeedQuery(groupId, 999),
            CancellationToken.None);

        _photos.Verify(x => x.GetFeedByGroupAsync(groupId, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyDictionaries_WhenNoReactionsOrComments()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var photo = Photo.Create(groupId, userId, "key", "image/jpeg", 100);

        _current.Setup(x => x.UserId).Returns(userId);

        _groups.Setup(x => x.IsMemberAsync(groupId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _photos.Setup(x => x.GetFeedByGroupAsync(groupId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { photo });

        _users.Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<InMoment.Domain.Users.User>());

        _storage.Setup(x => x.GetPublicUrl(It.IsAny<string>()))
            .Returns("url");

        _reactions.Setup(x => x.GetSummariesByPhotoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyDictionary<ReactionType, int>>());

        _reactions.Setup(x => x.GetUserReactionsByPhotoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ReactionType>());

        _comments.Setup(x => x.GetCountsByPhotoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int>());

        var handler = Create();

        var result = await handler.Handle(
            new GetGroupFeedQuery(groupId),
            CancellationToken.None);

        result[0].Reactions.Should().BeEmpty();
        result[0].MyReaction.Should().Be(ReactionType.None);
        result[0].CommentsCount.Should().Be(0);
    }
}