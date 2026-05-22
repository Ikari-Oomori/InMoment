using FluentAssertions;
using FluentValidation;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Auth.ResetPassword;
using InMoment.Domain.Common;
using InMoment.Domain.Security;
using InMoment.Domain.Users;
using Moq;

namespace InMoment.Application.Tests.Auth.ResetPassword;

public sealed class ResetPasswordHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPasswordResetTokenRepository> _resetTokens = new();
    private readonly Mock<IRefreshSessionRepository> _sessions = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IRefreshTokenService> _refreshTokens = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private ResetPasswordHandler Create()
        => new(
            _users.Object,
            _resetTokens.Object,
            _sessions.Object,
            _hasher.Object,
            _refreshTokens.Object,
            _uow.Object,
            new ResetPasswordValidator());

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenTokenEmpty()
    {
        var handler = Create();

        var act = () => handler.Handle(
            new ResetPasswordCommand(string.Empty, "Pass123!"),
            CancellationToken.None);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenPasswordTooShort()
    {
        var handler = Create();

        var act = () => handler.Handle(
            new ResetPasswordCommand("token", "123"),
            CancellationToken.None);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenResetTokenNotFound()
    {
        var rawToken = "raw-reset-token";
        var hash = "hash";

        _refreshTokens.Setup(x => x.HashToken(rawToken))
            .Returns(hash);

        _resetTokens.Setup(x => x.GetByTokenHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PasswordResetToken?)null);

        var handler = Create();

        var act = () => handler.Handle(
            new ResetPasswordCommand(rawToken, "Pass123!"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Invalid reset token.");
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenResetTokenExpired()
    {
        var rawToken = "raw-reset-token";
        var hash = "hash";
        var now = DateTime.UtcNow;

        var resetToken = PasswordResetToken.Create(
            Guid.NewGuid(),
            hash,
            now.AddDays(-2),
            now.AddMinutes(-1),
            null,
            null);

        _refreshTokens.Setup(x => x.HashToken(rawToken))
            .Returns(hash);

        _resetTokens.Setup(x => x.GetByTokenHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resetToken);

        var handler = Create();

        var act = () => handler.Handle(
            new ResetPasswordCommand(rawToken, "Pass123!"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Reset token expired or invalid.");
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenResetTokenAlreadyUsed()
    {
        var rawToken = "raw-reset-token";
        var hash = "hash";
        var now = DateTime.UtcNow;

        var resetToken = PasswordResetToken.Create(
            Guid.NewGuid(),
            hash,
            now.AddDays(-1),
            now.AddDays(1),
            null,
            null);

        resetToken.MarkUsed(now);

        _refreshTokens.Setup(x => x.HashToken(rawToken))
            .Returns(hash);

        _resetTokens.Setup(x => x.GetByTokenHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resetToken);

        var handler = Create();

        var act = () => handler.Handle(
            new ResetPasswordCommand(rawToken, "Pass123!"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Reset token expired or invalid.");
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenResetTokenRevoked()
    {
        var rawToken = "raw-reset-token";
        var hash = "hash";
        var now = DateTime.UtcNow;

        var resetToken = PasswordResetToken.Create(
            Guid.NewGuid(),
            hash,
            now.AddDays(-1),
            now.AddDays(1),
            null,
            null);

        resetToken.Revoke(now);

        _refreshTokens.Setup(x => x.HashToken(rawToken))
            .Returns(hash);

        _resetTokens.Setup(x => x.GetByTokenHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resetToken);

        var handler = Create();

        var act = () => handler.Handle(
            new ResetPasswordCommand(rawToken, "Pass123!"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Reset token expired or invalid.");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenUserNotFound()
    {
        var rawToken = "raw-reset-token";
        var hash = "hash";
        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();

        var resetToken = PasswordResetToken.Create(
            userId,
            hash,
            now.AddDays(-1),
            now.AddDays(1),
            null,
            null);

        _refreshTokens.Setup(x => x.HashToken(rawToken))
            .Returns(hash);

        _resetTokens.Setup(x => x.GetByTokenHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resetToken);

        _users.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = Create();

        var act = () => handler.Handle(
            new ResetPasswordCommand(rawToken, "Pass123!"),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("User not found.");
    }

    [Fact]
    public async Task Handle_ShouldRevokeResetToken_AndThrow_WhenUserDeactivated()
    {
        var rawToken = "raw-reset-token";
        var hash = "hash";
        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();

        var resetToken = PasswordResetToken.Create(
            userId,
            hash,
            now.AddDays(-1),
            now.AddDays(1),
            null,
            null);

        var user = User.Create(
            "user@test.com",
            "old-hash",
            "user",
            "Test",
            "User");
        SetUserId(user, userId);
        user.Deactivate();

        _refreshTokens.Setup(x => x.HashToken(rawToken))
            .Returns(hash);

        _resetTokens.Setup(x => x.GetByTokenHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resetToken);

        _users.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = Create();

        var act = () => handler.Handle(
            new ResetPasswordCommand(rawToken, "Pass123!"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Account is deactivated.");

        resetToken.IsRevoked.Should().BeTrue();
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldChangePassword_MarkTokenUsed_AndRevokeActiveSessions_WhenValid()
    {
        var rawToken = "raw-reset-token";
        var hash = "hash";
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var resetToken = PasswordResetToken.Create(
            userId,
            hash,
            now.AddDays(-1),
            now.AddDays(1),
            null,
            null);

        var user = User.Create(
            "user@test.com",
            "old-password-hash",
            "user",
            "Test",
            "User");
        SetUserId(user, userId);

        var activeSession1 = RefreshSession.Create(
            userId,
            "session-hash-1",
            now.AddDays(-5),
            now.AddDays(5),
            "device1",
            "ios",
            null,
            null,
            null,
            null,
            null,
            null);

        var activeSession2 = RefreshSession.Create(
            userId,
            "session-hash-2",
            now.AddDays(-3),
            now.AddDays(3),
            "device2",
            "android",
            null,
            null,
            null,
            null,
            null,
            null);

        var alreadyRevokedSession = RefreshSession.Create(
            userId,
            "session-hash-3",
            now.AddDays(-2),
            now.AddDays(2),
            "device3",
            "web",
            null,
            null,
            null,
            null,
            null,
            null);
        alreadyRevokedSession.Revoke("manual", now);

        _refreshTokens.Setup(x => x.HashToken(rawToken))
            .Returns(hash);

        _resetTokens.Setup(x => x.GetByTokenHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resetToken);

        _users.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _hasher.Setup(x => x.Hash("Pass123!"))
            .Returns("new-password-hash");

        _sessions.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { activeSession1, activeSession2, alreadyRevokedSession });

        var handler = Create();

        await handler.Handle(
            new ResetPasswordCommand(rawToken, "Pass123!"),
            CancellationToken.None);

        user.PasswordHash.Should().Be("new-password-hash");
        resetToken.IsUsed.Should().BeTrue();
        resetToken.UsedAtUtc.Should().NotBeNull();

        activeSession1.IsRevoked.Should().BeTrue();
        activeSession1.RevokeReason.Should().Be("password_reset");

        activeSession2.IsRevoked.Should().BeTrue();
        activeSession2.RevokeReason.Should().Be("password_reset");

        alreadyRevokedSession.IsRevoked.Should().BeTrue();
        alreadyRevokedSession.RevokeReason.Should().Be("manual");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static void SetUserId(User user, Guid id)
    {
        typeof(User)
            .GetProperty(nameof(User.Id))!
            .SetValue(user, id);
    }
}