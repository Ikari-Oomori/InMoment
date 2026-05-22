using FluentValidation;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Auth.Login;
using InMoment.Domain.Common;
using InMoment.Domain.Security;
using InMoment.Domain.Users;

namespace InMoment.Application.Tests.Auth.Login;

public sealed class LoginHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IRefreshSessionRepository> _sessions = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly Mock<IRefreshTokenService> _refreshTokens = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IGeoIpResolver> _geoIpResolver = new();

    private LoginHandler Create()
    => new(
        _users.Object,
        _sessions.Object,
        _hasher.Object,
        _tokens.Object,
        _refreshTokens.Object,
        _geoIpResolver.Object,
        _uow.Object,
        new LoginValidator());

    public LoginHandlerTests()
    {
        _geoIpResolver
            .Setup(x => x.ResolveAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeoIpLocationResult?)null);

        _sessions
            .Setup(x => x.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RefreshSession>());
    }

    [Fact]
    public async Task Handle_ShouldLoginByEmail_AndCreateSession()
    {
        var user = User.Create(
            "login-email@test.com",
            "stored-hash",
            "login_email",
            "Login",
            "Email");

        RefreshSession? createdSession = null;
        var refreshExpiry = DateTime.UtcNow.AddDays(30);

        _users.Setup(x => x.GetByEmailAsync("login-email@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _hasher.Setup(x => x.Verify("password123", "stored-hash"))
            .Returns(true);

        _tokens.Setup(x => x.CreateAccessToken(user.Id, user.UserName))
            .Returns("access-token");

        _refreshTokens.Setup(x => x.CreateToken()).Returns("raw-refresh");
        _refreshTokens.Setup(x => x.HashToken("raw-refresh")).Returns("refresh-hash");
        _refreshTokens.Setup(x => x.GetExpiryUtc()).Returns(refreshExpiry);

        _sessions.Setup(x => x.AddAsync(It.IsAny<RefreshSession>(), It.IsAny<CancellationToken>()))
            .Callback<RefreshSession, CancellationToken>((session, _) => createdSession = session)
            .Returns(Task.CompletedTask);

        var handler = Create();

        var result = await handler.Handle(
            new LoginCommand(
                "  login-email@test.com  ",
                "password123",
                "iPhone 15",
                "iOS",
                "10.10.10.10",
                "Safari"),
            CancellationToken.None);

        result.UserId.Should().Be(user.Id);
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("raw-refresh");
        result.RefreshTokenExpiresAtUtc.Should().Be(refreshExpiry);

        createdSession.Should().NotBeNull();
        createdSession!.UserId.Should().Be(user.Id);
        createdSession.TokenHash.Should().Be("refresh-hash");
        createdSession.DeviceName.Should().Be("iPhone 15");
        createdSession.Platform.Should().Be("iOS");
        createdSession.IpAddress.Should().Be("10.10.10.10");
        createdSession.UserAgent.Should().Be("Safari");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldLoginByUserName()
    {
        var user = User.Create(
            "login-username@test.com",
            "stored-hash",
            "nickname_1",
            "Nick",
            "Name");

        _users.Setup(x => x.GetByUserNameAsync("nickname_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _hasher.Setup(x => x.Verify("password123", "stored-hash"))
            .Returns(true);

        _tokens.Setup(x => x.CreateAccessToken(user.Id, user.UserName))
            .Returns("access-token");

        _refreshTokens.Setup(x => x.CreateToken()).Returns("raw-refresh");
        _refreshTokens.Setup(x => x.HashToken("raw-refresh")).Returns("refresh-hash");
        _refreshTokens.Setup(x => x.GetExpiryUtc()).Returns(DateTime.UtcNow.AddDays(30));

        _sessions.Setup(x => x.AddAsync(It.IsAny<RefreshSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = Create();

        var result = await handler.Handle(
            new LoginCommand(
                "nickname_1",
                "password123",
                null,
                null,
                null,
                null),
            CancellationToken.None);

        result.UserId.Should().Be(user.Id);

        _users.Verify(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _users.Verify(x => x.GetByUserNameAsync("nickname_1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUserNotFound()
    {
        _users.Setup(x => x.GetByEmailAsync("missing@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = Create();

        var act = () => handler.Handle(
            new LoginCommand(
                "missing@test.com",
                "password123",
                null,
                null,
                null,
                null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Аккаунт с таким email не найден.");

        _sessions.Verify(x => x.AddAsync(It.IsAny<RefreshSession>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenPasswordIsInvalid()
    {
        var user = User.Create(
            "bad-password@test.com",
            "stored-hash",
            "bad_password",
            "Bad",
            "Password");

        _users.Setup(x => x.GetByEmailAsync("bad-password@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _hasher.Setup(x => x.Verify("wrong-password", "stored-hash"))
            .Returns(false);

        var handler = Create();

        var act = () => handler.Handle(
            new LoginCommand(
                "bad-password@test.com",
                "wrong-password",
                null,
                null,
                null,
                null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Неверный пароль.");

        _sessions.Verify(x => x.AddAsync(It.IsAny<RefreshSession>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReactivateAccount_WhenAccountIsDeactivated()
    {
        var user = User.Create(
            "deactivated@test.com",
            "stored-hash",
            "deactivated_user",
            "Deactivated",
            "User");

        user.Deactivate();

        RefreshSession? createdSession = null;
        var refreshExpiry = DateTime.UtcNow.AddDays(30);

        _users.Setup(x => x.GetByEmailAsync("deactivated@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _hasher.Setup(x => x.Verify("password123", "stored-hash"))
            .Returns(true);

        _tokens.Setup(x => x.CreateAccessToken(user.Id, user.UserName))
            .Returns("access-token");

        _refreshTokens.Setup(x => x.CreateToken()).Returns("raw-refresh");
        _refreshTokens.Setup(x => x.HashToken("raw-refresh")).Returns("refresh-hash");
        _refreshTokens.Setup(x => x.GetExpiryUtc()).Returns(refreshExpiry);

        _sessions.Setup(x => x.AddAsync(It.IsAny<RefreshSession>(), It.IsAny<CancellationToken>()))
            .Callback<RefreshSession, CancellationToken>((session, _) => createdSession = session)
            .Returns(Task.CompletedTask);

        var handler = Create();

        var result = await handler.Handle(
            new LoginCommand(
                "deactivated@test.com",
                "password123",
                "device-1",
                "ios",
                null,
                null),
            CancellationToken.None);

        result.UserId.Should().Be(user.Id);
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("raw-refresh");
        result.RefreshTokenExpiresAtUtc.Should().Be(refreshExpiry);

        user.IsActive.Should().BeTrue();

        createdSession.Should().NotBeNull();
        createdSession!.UserId.Should().Be(user.Id);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowFluentValidationException_WhenCommandIsInvalid()
    {
        var handler = Create();

        var act = () => handler.Handle(
            new LoginCommand(
                "",
                "",
                null,
                null,
                null,
                null),
            CancellationToken.None);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();

        _users.VerifyNoOtherCalls();
        _sessions.VerifyNoOtherCalls();
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}