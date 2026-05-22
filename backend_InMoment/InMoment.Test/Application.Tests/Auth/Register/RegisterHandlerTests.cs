using DomainValidationException = InMoment.Domain.Common.ValidationException;
using FluentValidationException = FluentValidation.ValidationException;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Auth.Register;
using InMoment.Domain.Security;
using InMoment.Domain.Users;

namespace InMoment.Application.Tests.Auth.Register;

public sealed class RegisterHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IRefreshSessionRepository> _sessions = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly Mock<IRefreshTokenService> _refreshTokens = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private RegisterHandler Create()
        => new(
            _users.Object,
            _sessions.Object,
            _hasher.Object,
            _tokens.Object,
            _refreshTokens.Object,
            _uow.Object,
            new RegisterValidator());

    [Fact]
    public async Task Handle_ShouldCreateUser_AndSession_AndReturnTokens()
    {
        User? addedUser = null;
        RefreshSession? addedSession = null;
        var refreshExpiry = DateTime.UtcNow.AddDays(30);

        _users.Setup(x => x.EmailExistsAsync("new-user@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _users.Setup(x => x.UserNameExistsAsync("new_user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _hasher.Setup(x => x.Hash("password123"))
            .Returns("password-hash");

        _users.Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => addedUser = user)
            .Returns(Task.CompletedTask);

        _tokens.Setup(x => x.CreateAccessToken(It.IsAny<Guid>(), "new_user"))
            .Returns("access-token");

        _refreshTokens.Setup(x => x.CreateToken()).Returns("raw-refresh");
        _refreshTokens.Setup(x => x.HashToken("raw-refresh")).Returns("refresh-hash");
        _refreshTokens.Setup(x => x.GetExpiryUtc()).Returns(refreshExpiry);

        _sessions.Setup(x => x.AddAsync(It.IsAny<RefreshSession>(), It.IsAny<CancellationToken>()))
            .Callback<RefreshSession, CancellationToken>((session, _) => addedSession = session)
            .Returns(Task.CompletedTask);

        var handler = Create();

        var result = await handler.Handle(
            new RegisterCommand(
                "new-user@test.com",
                "password123",
                "Anna",
                "Petrova",
                "new_user"),
            CancellationToken.None);

        addedUser.Should().NotBeNull();
        addedUser!.Email.Should().Be("new-user@test.com");
        addedUser.UserName.Should().Be("new_user");
        addedUser.FirstName.Should().Be("Anna");
        addedUser.LastName.Should().Be("Petrova");
        addedUser.PasswordHash.Should().Be("password-hash");

        addedSession.Should().NotBeNull();
        addedSession!.UserId.Should().Be(addedUser.Id);
        addedSession.TokenHash.Should().Be("refresh-hash");
        addedSession.DeviceName.Should().Be("register");

        result.UserId.Should().Be(addedUser.Id);
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("raw-refresh");
        result.RefreshTokenExpiresAtUtc.Should().Be(refreshExpiry);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenEmailAlreadyExists()
    {
        _users.Setup(x => x.EmailExistsAsync("exists@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = Create();

        var act = () => handler.Handle(
            new RegisterCommand(
                "exists@test.com",
                "password123",
                "Anna",
                "Petrova",
                "anna_pet"),
            CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("Email is already used.");

        _users.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _sessions.Verify(x => x.AddAsync(It.IsAny<RefreshSession>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenUserNameAlreadyExists()
    {
        _users.Setup(x => x.EmailExistsAsync("new@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _users.Setup(x => x.UserNameExistsAsync("taken_name", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = Create();

        var act = () => handler.Handle(
            new RegisterCommand(
                "new@test.com",
                "password123",
                "Anna",
                "Petrova",
                "taken_name"),
            CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("Nickname is already used.");

        _users.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _sessions.Verify(x => x.AddAsync(It.IsAny<RefreshSession>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowFluentValidationException_WhenCommandIsInvalid()
    {
        var handler = Create();

        var act = () => handler.Handle(
            new RegisterCommand(
                "bad-email",
                "123",
                "",
                "",
                "x"),
            CancellationToken.None);

        await act.Should().ThrowAsync<FluentValidationException>();

        _users.Verify(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _sessions.Verify(x => x.AddAsync(It.IsAny<RefreshSession>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}