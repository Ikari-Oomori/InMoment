using FluentAssertions;
using InMoment.Domain.Common;
using InMoment.Domain.Security;

namespace InMoment.Domain.Tests.Security;

public sealed class PasswordResetTokenTests
{
    [Fact]
    public void Create_ShouldThrowValidationException_WhenUserIdEmpty()
    {
        var act = () => PasswordResetToken.Create(
            Guid.Empty,
            "token-hash",
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(1),
            null,
            null);

        act.Should().Throw<ValidationException>()
            .WithMessage("UserId is required.");
    }

    [Fact]
    public void Create_ShouldThrowValidationException_WhenTokenHashEmpty()
    {
        var act = () => PasswordResetToken.Create(
            Guid.NewGuid(),
            "",
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(1),
            null,
            null);

        act.Should().Throw<ValidationException>()
            .WithMessage("Token hash is required.");
    }

    [Fact]
    public void Create_ShouldThrowValidationException_WhenExpiryInvalid()
    {
        var now = DateTime.UtcNow;

        var act = () => PasswordResetToken.Create(
            Guid.NewGuid(),
            "token-hash",
            now,
            now,
            null,
            null);

        act.Should().Throw<ValidationException>()
            .WithMessage("Password reset token expiry is invalid.");
    }

    [Fact]
    public void Create_ShouldCreateToken_WhenValid()
    {
        var now = DateTime.UtcNow;

        var token = PasswordResetToken.Create(
            Guid.NewGuid(),
            "token-hash",
            now,
            now.AddHours(1),
            "127.0.0.1",
            "test-agent");

        token.Id.Should().NotBe(Guid.Empty);
        token.UserId.Should().NotBe(Guid.Empty);
        token.TokenHash.Should().Be("token-hash");
        token.CreatedAtUtc.Should().Be(now);
        token.ExpiresAtUtc.Should().Be(now.AddHours(1));
        token.RequestedByIp.Should().Be("127.0.0.1");
        token.RequestedByUserAgent.Should().Be("test-agent");
        token.IsUsed.Should().BeFalse();
        token.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public void IsActive_ShouldReturnTrue_WhenNotUsedNotRevokedAndNotExpired()
    {
        var now = DateTime.UtcNow;

        var token = PasswordResetToken.Create(
            Guid.NewGuid(),
            "token-hash",
            now.AddMinutes(-5),
            now.AddMinutes(10),
            null,
            null);

        token.IsActive(now).Should().BeTrue();
    }

    [Fact]
    public void IsActive_ShouldReturnFalse_WhenExpired()
    {
        var now = DateTime.UtcNow;

        var token = PasswordResetToken.Create(
            Guid.NewGuid(),
            "token-hash",
            now.AddHours(-2),
            now.AddMinutes(-1),
            null,
            null);

        token.IsActive(now).Should().BeFalse();
    }

    [Fact]
    public void IsActive_ShouldReturnFalse_WhenUsed()
    {
        var now = DateTime.UtcNow;

        var token = PasswordResetToken.Create(
            Guid.NewGuid(),
            "token-hash",
            now.AddMinutes(-5),
            now.AddMinutes(10),
            null,
            null);

        token.MarkUsed(now);

        token.IsActive(now).Should().BeFalse();
    }

    [Fact]
    public void IsActive_ShouldReturnFalse_WhenRevoked()
    {
        var now = DateTime.UtcNow;

        var token = PasswordResetToken.Create(
            Guid.NewGuid(),
            "token-hash",
            now.AddMinutes(-5),
            now.AddMinutes(10),
            null,
            null);

        token.Revoke(now);

        token.IsActive(now).Should().BeFalse();
    }

    [Fact]
    public void MarkUsed_ShouldSetUsedAtUtc_WhenValid()
    {
        var now = DateTime.UtcNow;

        var token = PasswordResetToken.Create(
            Guid.NewGuid(),
            "token-hash",
            now.AddMinutes(-5),
            now.AddMinutes(10),
            null,
            null);

        token.MarkUsed(now);

        token.IsUsed.Should().BeTrue();
        token.UsedAtUtc.Should().Be(now);
    }

    [Fact]
    public void MarkUsed_ShouldThrowValidationException_WhenAlreadyUsed()
    {
        var now = DateTime.UtcNow;

        var token = PasswordResetToken.Create(
            Guid.NewGuid(),
            "token-hash",
            now.AddMinutes(-5),
            now.AddMinutes(10),
            null,
            null);

        token.MarkUsed(now);

        var act = () => token.MarkUsed(now.AddMinutes(1));

        act.Should().Throw<ValidationException>()
            .WithMessage("Reset token already used.");
    }

    [Fact]
    public void MarkUsed_ShouldThrowValidationException_WhenRevoked()
    {
        var now = DateTime.UtcNow;

        var token = PasswordResetToken.Create(
            Guid.NewGuid(),
            "token-hash",
            now.AddMinutes(-5),
            now.AddMinutes(10),
            null,
            null);

        token.Revoke(now);

        var act = () => token.MarkUsed(now.AddMinutes(1));

        act.Should().Throw<ValidationException>()
            .WithMessage("Reset token is revoked.");
    }

    [Fact]
    public void Revoke_ShouldSetRevokedAtUtc_WhenNotRevoked()
    {
        var now = DateTime.UtcNow;

        var token = PasswordResetToken.Create(
            Guid.NewGuid(),
            "token-hash",
            now.AddMinutes(-5),
            now.AddMinutes(10),
            null,
            null);

        token.Revoke(now);

        token.IsRevoked.Should().BeTrue();
        token.RevokedAtUtc.Should().Be(now);
    }

    [Fact]
    public void Revoke_ShouldBeIdempotent_WhenAlreadyRevoked()
    {
        var now = DateTime.UtcNow;

        var token = PasswordResetToken.Create(
            Guid.NewGuid(),
            "token-hash",
            now.AddMinutes(-5),
            now.AddMinutes(10),
            null,
            null);

        token.Revoke(now);
        var firstRevokedAt = token.RevokedAtUtc;

        token.Revoke(now.AddMinutes(1));

        token.RevokedAtUtc.Should().Be(firstRevokedAt);
    }
}