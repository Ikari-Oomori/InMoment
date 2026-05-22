using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Abstractions.Storage;
using InMoment.Application.Features.Media.GetPhotoDetails;
using InMoment.Domain.Common;
using InMoment.Domain.Media;
using InMoment.Domain.Users;
using Moq;

namespace InMoment.Application.Tests.Media.GetPhotoDetails;

public sealed class GetPhotoDetailsHandlerTests
{
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IPhotoRepository> _photos = new();
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IReactionRepository> _reactions = new();
    private readonly Mock<ICommentRepository> _comments = new();
    private readonly Mock<IBlockedUserRepository> _blocks = new();
    private readonly Mock<IFileStorage> _storage = new();

    private GetPhotoDetailsHandler Create()
        => new(
            _current.Object,
            _photos.Object,
            _groups.Object,
            _users.Object,
            _reactions.Object,
            _comments.Object,
            _blocks.Object,
            _storage.Object);

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenPhotoIdEmpty()
    {
        var handler = Create();

        var act = () => handler.Handle(
            new GetPhotoDetailsQuery(Guid.Empty),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("PhotoId is required.");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenPhotoMissing()
    {
        var photoId = Guid.NewGuid();

        _photos.Setup(x => x.GetByIdAsync(photoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Photo?)null);

        var handler = Create();

        var act = () => handler.Handle(
            new GetPhotoDetailsQuery(photoId),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Photo not found.");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenPhotoDeleted()
    {
        var currentUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var photo = Photo.Create(
            groupId,
            currentUserId,
            "groups/g/photos/u/file.jpg",
            "image/jpeg",
            123);

        typeof(Photo)
            .GetProperty(nameof(Photo.IsDeleted))!
            .SetValue(photo, true);

        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);

        var handler = Create();

        var act = () => handler.Handle(
            new GetPhotoDetailsQuery(photo.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Photo not found.");
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenCurrentUserNotGroupMember()
    {
        var currentUserId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var photo = Photo.Create(
            groupId,
            authorId,
            "groups/g/photos/u/file.jpg",
            "image/jpeg",
            123);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(groupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = Create();

        var act = () => handler.Handle(
            new GetPhotoDetailsQuery(photo.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not an active member of this group.");
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenBlockedEitherDirection()
    {
        var currentUserId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var photo = Photo.Create(
            groupId,
            authorId,
            "groups/g/photos/u/file.jpg",
            "image/jpeg",
            123);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(groupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = Create();

        var act = () => handler.Handle(
            new GetPhotoDetailsQuery(photo.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Взаимодействие с этим пользователем недоступно.");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenAuthorMissing()
    {
        var currentUserId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var photo = Photo.Create(
            groupId,
            authorId,
            "groups/g/photos/u/file.jpg",
            "image/jpeg",
            123);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(groupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _users.Setup(x => x.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = Create();

        var act = () => handler.Handle(
            new GetPhotoDetailsQuery(photo.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Author not found.");
    }

    [Fact]
    public async Task Handle_ShouldReturnPhotoDetails_WhenEverythingIsValid()
    {
        var currentUserId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var photo = Photo.Create(
            groupId,
            authorId,
            "groups/g1/photos/u1/file.jpg",
            "image/jpeg",
            1024,
            "  hello caption  ");

        var author = User.Create(
            "author@test.com",
            "hash",
            "author.user",
            "Author",
            "User");

        typeof(User)
            .GetProperty(nameof(User.Id))!
            .SetValue(author, authorId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(groupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _users.Setup(x => x.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        _reactions.Setup(x => x.GetSummaryAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<ReactionType, int>
            {
                [ReactionType.Heart] = 2,
                [ReactionType.Wow] = 1
            });
        _reactions.Setup(x => x.GetByPhotoAndUserAsync(photo.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Reaction.Create(photo.Id, currentUserId, ReactionType.Wow));
        _comments.Setup(x => x.GetCountsByPhotoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int>
            {
                [photo.Id] = 5
            });
        _storage.Setup(x => x.GetPublicUrl(photo.StorageKey))
            .Returns("https://cdn.example.com/photo.jpg");

        var handler = Create();

        var result = await handler.Handle(
            new GetPhotoDetailsQuery(photo.Id),
            CancellationToken.None);

        result.PhotoId.Should().Be(photo.Id);
        result.GroupId.Should().Be(groupId);
        result.AuthorId.Should().Be(authorId);
        result.AuthorUserName.Should().Be("author.user");
        result.AuthorFirstName.Should().Be("Author");
        result.AuthorLastName.Should().Be("User");
        result.Url.Should().Be("https://cdn.example.com/photo.jpg");
        result.ContentType.Should().Be("image/jpeg");
        result.SizeBytes.Should().Be(1024);
        result.Caption.Should().Be("hello caption");
        result.IsMine.Should().BeFalse();
        result.CanEdit.Should().BeFalse();
        result.CanDelete.Should().BeFalse();
        result.MyReaction.Should().Be(ReactionType.Wow);
        result.CommentsCount.Should().Be(5);
        result.Reactions.Should().Contain(x => x.Type == ReactionType.Heart && x.Count == 2);
        result.Reactions.Should().Contain(x => x.Type == ReactionType.Wow && x.Count == 1);
    }

    [Fact]
    public async Task Handle_ShouldReturnDefaults_WhenNoReactionsAndNoComments()
    {
        var currentUserId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var photo = Photo.Create(
            groupId,
            authorId,
            "groups/g1/photos/u1/file.jpg",
            "image/jpeg",
            1024);

        var author = User.Create(
            "author2@test.com",
            "hash",
            "author2.user",
            "Author2",
            "User2");

        typeof(User)
            .GetProperty(nameof(User.Id))!
            .SetValue(author, authorId);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _groups.Setup(x => x.IsMemberAsync(groupId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _blocks.Setup(x => x.ExistsEitherDirectionAsync(currentUserId, authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _users.Setup(x => x.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        _reactions.Setup(x => x.GetSummaryAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<ReactionType, int>());
        _reactions.Setup(x => x.GetByPhotoAndUserAsync(photo.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Reaction?)null);
        _comments.Setup(x => x.GetCountsByPhotoIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int>());
        _storage.Setup(x => x.GetPublicUrl(photo.StorageKey))
            .Returns("https://cdn.example.com/photo2.jpg");

        var handler = Create();

        var result = await handler.Handle(
            new GetPhotoDetailsQuery(photo.Id),
            CancellationToken.None);

        result.MyReaction.Should().Be(ReactionType.None);
        result.CommentsCount.Should().Be(0);
        result.Reactions.Should().BeEmpty();
        result.Caption.Should().BeNull();
        result.IsMine.Should().BeFalse();
        result.CanEdit.Should().BeFalse();
        result.CanDelete.Should().BeFalse();
    }
}