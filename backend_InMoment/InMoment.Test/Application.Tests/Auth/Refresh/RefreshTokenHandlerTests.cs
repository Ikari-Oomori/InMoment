using DomainValidationException = InMoment.Domain.Common.ValidationException;
using FluentValidation;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Auth.Refresh;
using InMoment.Domain.Common;
using InMoment.Domain.Security;
using InMoment.Domain.Users;
using Moq;

namespace InMoment.Application.Tests.Auth.Refresh;

public sealed class RefreshTokenHandlerTests
{
    private readonly Mock<IRefreshSessionRepository> _sessions = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly Mock<IRefreshTokenService> _refreshTokens = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private RefreshTokenHandler Create()
        => new(
            _sessions.Object,
            _users.Object,
            _tokens.Object,
            _refreshTokens.Object,
            _uow.Object,
            new RefreshTokenValidator());

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenRefreshTokenEmpty()
    {
        var handler = Create();

        var act = () => handler.Handle(
            new RefreshTokenCommand(string.Empty),
            CancellationToken.None);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenSessionNotFound()
    {
        var rawToken = "raw-refresh-token";
        var hash = "hash";

        _refreshTokens.Setup(x => x.HashToken(rawToken))
            .Returns(hash);

        _sessions.Setup(x => x.GetByTokenHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshSession?)null);

        var handler = Create();

        var act = () => handler.Handle(
            new RefreshTokenCommand(rawToken),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Invalid refresh token.");
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenSessionExpired()
    {
        var rawToken = "raw-refresh-token";
        var hash = "hash";
        var now = DateTime.UtcNow;

        var session = RefreshSession.Create(
            Guid.NewGuid(),
            hash,
            now.AddDays(-10),
            now.AddMinutes(-1),
            "device",
            "platform",
            null,
            null,
            null,
            null,
            null,
            null);

        _refreshTokens.Setup(x => x.HashToken(rawToken))
            .Returns(hash);

        _sessions.Setup(x => x.GetByTokenHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = Create();

        var act = () => handler.Handle(
            new RefreshTokenCommand(rawToken),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Refresh session expired or revoked.");
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenSessionRevoked()
    {
        var rawToken = "raw-refresh-token";
        var hash = "hash";
        var now = DateTime.UtcNow;

        var session = RefreshSession.Create(
            Guid.NewGuid(),
            hash,
            now.AddDays(-1),
            now.AddDays(10),
            "device",
            "platform",
            null,
            null,
            null,
            null,
            null,
            null);

        session.Revoke("manual", now);

        _refreshTokens.Setup(x => x.HashToken(rawToken))
            .Returns(hash);

        _sessions.Setup(x => x.GetByTokenHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = Create();

        var act = () => handler.Handle(
            new RefreshTokenCommand(rawToken),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Refresh session expired or revoked.");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenUserNotFound()
    {
        var rawToken = "raw-refresh-token";
        var hash = "hash";
        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();

        var session = RefreshSession.Create(
            userId,
            hash,
            now.AddDays(-1),
            now.AddDays(10),
            "device",
            "platform",
            null,
            null,
            null,
            null,
            null,
            null);

        _refreshTokens.Setup(x => x.HashToken(rawToken))
            .Returns(hash);

        _sessions.Setup(x => x.GetByTokenHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _users.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = Create();

        var act = () => handler.Handle(
            new RefreshTokenCommand(rawToken),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("User not found.");
    }

    [Fact]
    public async Task Handle_ShouldRevokeSession_AndThrow_WhenUserDeactivated()
    {
        var rawToken = "raw-refresh-token";
        var hash = "hash";
        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();

        var session = RefreshSession.Create(
            userId,
            hash,
            now.AddDays(-1),
            now.AddDays(10),
            "device",
            "platform",
            null,
            null,
            null,
            null,
            null,
            null);

        var user = User.Create(
            "user@test.com",
            "hash",
            "user",
            "Test",
            "User");

        user.Deactivate();
        SetUserId(user, userId);

        _refreshTokens.Setup(x => x.HashToken(rawToken))
            .Returns(hash);

        _sessions.Setup(x => x.GetByTokenHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _users.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = Create();

        var act = () => handler.Handle(
            new RefreshTokenCommand(rawToken),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Account is deactivated.");

        session.IsRevoked.Should().BeTrue();
        session.RevokeReason.Should().Be("account_deactivated");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldRotateSession_AndReturnNewTokens_WhenValid()
    {
        var rawToken = "raw-refresh-token";
        var oldHash = "old-hash";
        var newRawToken = "new-raw-refresh-token";
        var newHash = "new-hash";
        var newExpiry = DateTime.UtcNow.AddDays(30);
        var userId = Guid.NewGuid();

        var session = RefreshSession.Create(
            userId,
            oldHash,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(10),
            "device",
            "platform",
            null,
            null,
            null,
            null,
            null,
            null);

        var oldTokenHash = session.TokenHash;
        var oldExpiry = session.ExpiresAtUtc;

        var user = User.Create(
            "user@test.com",
            "password-hash",
            "user",
            "Test",
            "User");

        SetUserId(user, userId);

        _refreshTokens.Setup(x => x.HashToken(rawToken))
            .Returns(oldHash);

        _sessions.Setup(x => x.GetByTokenHashAsync(oldHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _users.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _refreshTokens.Setup(x => x.CreateToken())
            .Returns(newRawToken);

        _refreshTokens.Setup(x => x.HashToken(newRawToken))
            .Returns(newHash);

        _refreshTokens.Setup(x => x.GetExpiryUtc())
            .Returns(newExpiry);

        _tokens.Setup(x => x.CreateAccessToken(userId, user.UserName))
            .Returns("new-access-token");

        var handler = Create();

        var result = await handler.Handle(
            new RefreshTokenCommand(rawToken),
            CancellationToken.None);

        result.AccessToken.Should().Be("new-access-token");
        result.RefreshToken.Should().Be(newRawToken);
        result.RefreshTokenExpiresAtUtc.Should().Be(newExpiry);

        session.TokenHash.Should().Be(newHash);
        session.TokenHash.Should().NotBe(oldTokenHash);
        session.ExpiresAtUtc.Should().Be(newExpiry);
        session.ExpiresAtUtc.Should().NotBe(oldExpiry);
        session.LastUsedAtUtc.Should().NotBeNull();
        session.IsRevoked.Should().BeFalse();

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldRejectOldToken_AfterSuccessfulRotation()
    {
        var rawToken = "raw-refresh-token";
        var oldHash = "old-hash";
        var newRawToken = "new-raw-refresh-token";
        var newHash = "new-hash";
        var newExpiry = DateTime.UtcNow.AddDays(30);
        var userId = Guid.NewGuid();

        var session = RefreshSession.Create(
            userId,
            oldHash,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(10),
            "device",
            "platform",
            null,
            null,
            null,
            null,
            null,
            null);

        var user = User.Create(
            "user@test.com",
            "password-hash",
            "user",
            "Test",
            "User");

        SetUserId(user, userId);

        _refreshTokens.Setup(x => x.HashToken(rawToken))
            .Returns(oldHash);

        _sessions.SetupSequence(x => x.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session)
            .ReturnsAsync((RefreshSession?)null);

        _users.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _refreshTokens.Setup(x => x.CreateToken())
            .Returns(newRawToken);

        _refreshTokens.Setup(x => x.HashToken(newRawToken))
            .Returns(newHash);

        _refreshTokens.Setup(x => x.GetExpiryUtc())
            .Returns(newExpiry);

        _tokens.Setup(x => x.CreateAccessToken(userId, user.UserName))
            .Returns("new-access-token");

        var handler = Create();

        var first = await handler.Handle(
            new RefreshTokenCommand(rawToken),
            CancellationToken.None);

        first.RefreshToken.Should().Be(newRawToken);

        var act = () => handler.Handle(
            new RefreshTokenCommand(rawToken),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Invalid refresh token.");
    }

    private static void SetUserId(User user, Guid id)
    {
        typeof(User)
            .GetProperty(nameof(User.Id))!
            .SetValue(user, id);
    }
}