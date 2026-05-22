using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Users.SetProfilePhoto;
using InMoment.Domain.Common;
using InMoment.Domain.Users;

namespace InMoment.Application.Tests.Users.SetProfilePhoto;

public sealed class SetProfilePhotoHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _current = new();

    private SetProfilePhotoHandler Create()
        => new(
            _users.Object,
            _uow.Object,
            _current.Object);

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenCurrentUserIsEmpty()
    {
        _current.SetupGet(x => x.UserId).Returns(Guid.Empty);

        var handler = Create();

        var act = () => handler.Handle(
            new SetProfilePhotoCommand("https://cdn.example.com/avatars/me.jpg"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Unauthorized.");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenUserNotFound()
    {
        var currentUserId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = Create();

        var act = () => handler.Handle(
            new SetProfilePhotoCommand("https://cdn.example.com/avatars/me.jpg"),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("User not found.");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenUrlInvalid()
    {
        var currentUserId = Guid.NewGuid();

        var user = User.Create(
            "photo@test.com",
            "hash",
            "photo_user",
            "Photo",
            "User");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = Create();

        var act = () => handler.Handle(
            new SetProfilePhotoCommand("not-a-valid-url"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Profile photo url is invalid.");

        user.ProfilePhotoUrl.Should().BeNull();
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenUrlRelative()
    {
        var currentUserId = Guid.NewGuid();

        var user = User.Create(
            "photo_rel@test.com",
            "hash",
            "photo_user_rel",
            "Photo",
            "User");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = Create();

        var act = () => handler.Handle(
            new SetProfilePhotoCommand("/avatars/me.jpg"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Profile photo url is invalid.");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldSetProfilePhoto_WhenUserExists()
    {
        var currentUserId = Guid.NewGuid();

        var user = User.Create(
            "photo@test.com",
            "hash",
            "photo_user",
            "Photo",
            "User");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = Create();

        await handler.Handle(
            new SetProfilePhotoCommand("https://cdn.example.com/avatars/me.jpg"),
            CancellationToken.None);

        user.ProfilePhotoUrl.Should().Be("https://cdn.example.com/avatars/me.jpg");
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldAllowHttpUrl()
    {
        var currentUserId = Guid.NewGuid();

        var user = User.Create(
            "photo_http@test.com",
            "hash",
            "photo_user_http",
            "Photo",
            "User");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = Create();

        await handler.Handle(
            new SetProfilePhotoCommand("http://localhost:9000/inmoment/users/me.jpg"),
            CancellationToken.None);

        user.ProfilePhotoUrl.Should().Be("http://localhost:9000/inmoment/users/me.jpg");
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldAllowNullUrl_AndClearProfilePhoto()
    {
        var currentUserId = Guid.NewGuid();

        var user = User.Create(
            "photo2@test.com",
            "hash",
            "photo_user_2",
            "Photo",
            "User");

        user.SetProfilePhoto("https://cdn.example.com/avatars/old.jpg");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = Create();

        await handler.Handle(
            new SetProfilePhotoCommand(null),
            CancellationToken.None);

        user.ProfilePhotoUrl.Should().BeNull();
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldAllowWhitespaceUrl_AndClearProfilePhoto()
    {
        var currentUserId = Guid.NewGuid();

        var user = User.Create(
            "photo3@test.com",
            "hash",
            "photo_user_3",
            "Photo",
            "User");

        user.SetProfilePhoto("https://cdn.example.com/avatars/old.jpg");

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = Create();

        await handler.Handle(
            new SetProfilePhotoCommand("   "),
            CancellationToken.None);

        user.ProfilePhotoUrl.Should().BeNull();
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}