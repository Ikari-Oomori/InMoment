using FluentValidation;
using InMoment.Application.Abstractions.Communication;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Auth.ForgotPassword;
using InMoment.Domain.Security;
using InMoment.Domain.Users;
using Microsoft.Extensions.Logging;

namespace InMoment.Application.Tests.Auth.ForgotPassword;

public sealed class ForgotPasswordHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPasswordResetTokenRepository> _tokens = new();
    private readonly Mock<IRefreshTokenService> _refreshTokens = new();
    private readonly Mock<IPasswordRecoverySender> _sender = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ILogger<ForgotPasswordHandler>> _logger = new();

    private ForgotPasswordHandler Create()
        => new(
            _users.Object,
            _tokens.Object,
            _refreshTokens.Object,
            _sender.Object,
            _uow.Object,
            new ForgotPasswordValidator(),
            _logger.Object);

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenUserDoesNotExist()
    {
        _users.Setup(x => x.GetByEmailAsync("missing@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = Create();

        await handler.Handle(
            new ForgotPasswordCommand(
                "  missing@test.com  ",
                "10.0.0.1",
                "Browser"),
            CancellationToken.None);

        _tokens.Verify(x => x.GetActiveByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _tokens.Verify(x => x.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _sender.Verify(x => x.SendResetPasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenUserIsInactive()
    {
        var user = User.Create(
            "inactive@test.com",
            "hash",
            "inactive_user",
            "Inactive",
            "User");

        user.Deactivate();

        _users.Setup(x => x.GetByEmailAsync("inactive@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = Create();

        await handler.Handle(
            new ForgotPasswordCommand(
                "inactive@test.com",
                "10.0.0.1",
                "Browser"),
            CancellationToken.None);

        _tokens.Verify(x => x.GetActiveByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _tokens.Verify(x => x.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _sender.Verify(x => x.SendResetPasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRevokeOldTokens_CreateNewToken_AndSendRecoveryMessage()
    {
        var user = User.Create(
            "forgot@test.com",
            "hash",
            "forgot_user",
            "Anna",
            "Petrova");

        var activeToken1 = PasswordResetToken.Create(
            user.Id,
            "old-hash-1",
            DateTime.UtcNow.AddMinutes(-20),
            DateTime.UtcNow.AddMinutes(40),
            "1.1.1.1",
            "Agent1");

        var activeToken2 = PasswordResetToken.Create(
            user.Id,
            "old-hash-2",
            DateTime.UtcNow.AddMinutes(-10),
            DateTime.UtcNow.AddMinutes(50),
            "2.2.2.2",
            "Agent2");

        PasswordResetToken? addedToken = null;

        _users.Setup(x => x.GetByEmailAsync("forgot@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _tokens.Setup(x => x.GetActiveByUserIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PasswordResetToken> { activeToken1, activeToken2 });

        _refreshTokens.Setup(x => x.CreateToken()).Returns("raw-reset-token");
        _refreshTokens.Setup(x => x.HashToken("raw-reset-token")).Returns("new-reset-hash");

        _tokens.Setup(x => x.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()))
            .Callback<PasswordResetToken, CancellationToken>((token, _) => addedToken = token)
            .Returns(Task.CompletedTask);

        var handler = Create();

        await handler.Handle(
            new ForgotPasswordCommand(
                "  FORGOT@test.com  ",
                "10.0.0.5",
                "Mozilla"),
            CancellationToken.None);

        activeToken1.IsRevoked.Should().BeTrue();
        activeToken2.IsRevoked.Should().BeTrue();

        addedToken.Should().NotBeNull();
        addedToken!.UserId.Should().Be(user.Id);
        addedToken.TokenHash.Should().Be("new-reset-hash");
        addedToken.RequestedByIp.Should().Be("10.0.0.5");
        addedToken.RequestedByUserAgent.Should().Be("Mozilla");
        addedToken.ExpiresAtUtc.Should().BeAfter(addedToken.CreatedAtUtc);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        _sender.Verify(x => x.SendResetPasswordAsync(
                "forgot@test.com",
                "Anna Petrova",
                "raw-reset-token",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowFluentValidationException_WhenCommandIsInvalid()
    {
        var handler = Create();

        var act = () => handler.Handle(
            new ForgotPasswordCommand(
                "bad-email",
                null,
                null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();

        _tokens.Verify(x => x.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _sender.Verify(x => x.SendResetPasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}