using FluentAssertions;
using InMoment.Domain.Common;
using InMoment.Domain.Security;

namespace InMoment.Domain.Tests.Security;

public sealed class RefreshSessionTests
{
    [Fact]
    public void Create_ShouldThrowValidationException_WhenUserIdEmpty()
    {
        var now = DateTime.UtcNow;

        var act = () => RefreshSession.Create(
            Guid.Empty,
            "hash",
            now,
            now.AddDays(1),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        act.Should().Throw<ValidationException>()
            .WithMessage("UserId is required.");
    }

    [Fact]
    public void Create_ShouldThrowValidationException_WhenTokenHashEmpty()
    {
        var now = DateTime.UtcNow;

        var act = () => RefreshSession.Create(
            Guid.NewGuid(),
            "",
            now,
            now.AddDays(1),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        act.Should().Throw<ValidationException>()
            .WithMessage("Token hash is required.");
    }

    [Fact]
    public void Create_ShouldThrowValidationException_WhenExpiryInvalid()
    {
        var now = DateTime.UtcNow;

        var act = () => RefreshSession.Create(
            Guid.NewGuid(),
            "hash",
            now,
            now,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        act.Should().Throw<ValidationException>()
            .WithMessage("Refresh token expiry is invalid.");
    }

    [Fact]
    public void Create_ShouldCreateSession_WhenValid()
    {
        var now = DateTime.UtcNow;

        var session = RefreshSession.Create(
            Guid.NewGuid(),
            "hash",
            now,
            now.AddDays(30),
            " iPhone ",
            " ios ",
            " 127.0.0.1 ",
            " test-agent ",
            null,
            null,
            null,
            null);

        session.Id.Should().NotBe(Guid.Empty);
        session.UserId.Should().NotBe(Guid.Empty);
        session.TokenHash.Should().Be("hash");
        session.CreatedAtUtc.Should().Be(now);
        session.ExpiresAtUtc.Should().Be(now.AddDays(30));
        session.DeviceName.Should().Be("iPhone");
        session.Platform.Should().Be("ios");
        session.IpAddress.Should().Be("127.0.0.1");
        session.UserAgent.Should().Be("test-agent");
        session.IsRevoked.Should().BeFalse();
        session.LastUsedAtUtc.Should().BeNull();
        session.RevokedAtUtc.Should().BeNull();
    }

    [Fact]
    public void IsActive_ShouldReturnTrue_WhenNotExpiredAndNotRevoked()
    {
        var now = DateTime.UtcNow;

        var session = RefreshSession.Create(
            Guid.NewGuid(),
            "hash",
            now.AddHours(-1),
            now.AddHours(1),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        session.IsActive(now).Should().BeTrue();
    }

    [Fact]
    public void IsActive_ShouldReturnFalse_WhenExpired()
    {
        var now = DateTime.UtcNow;

        var session = RefreshSession.Create(
            Guid.NewGuid(),
            "hash",
            now.AddDays(-2),
            now.AddMinutes(-1),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        session.IsActive(now).Should().BeFalse();
    }

    [Fact]
    public void IsActive_ShouldReturnFalse_WhenRevoked()
    {
        var now = DateTime.UtcNow;

        var session = RefreshSession.Create(
            Guid.NewGuid(),
            "hash",
            now.AddHours(-1),
            now.AddHours(1),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        session.Revoke("logout", now);

        session.IsActive(now).Should().BeFalse();
    }

    [Fact]
    public void MarkUsed_ShouldSetLastUsedAtUtc_WhenValid()
    {
        var now = DateTime.UtcNow;

        var session = RefreshSession.Create(
            Guid.NewGuid(),
            "hash",
            now.AddHours(-1),
            now.AddHours(1),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        session.MarkUsed(now);

        session.LastUsedAtUtc.Should().Be(now);
    }

    [Fact]
    public void MarkUsed_ShouldThrowValidationException_WhenRevoked()
    {
        var now = DateTime.UtcNow;

        var session = RefreshSession.Create(
            Guid.NewGuid(),
            "hash",
            now.AddHours(-1),
            now.AddHours(1),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        session.Revoke("logout", now);

        var act = () => session.MarkUsed(now.AddMinutes(1));

        act.Should().Throw<ValidationException>()
            .WithMessage("Session is revoked.");
    }

    [Fact]
    public void Rotate_ShouldUpdateHashExpiryAndLastUsed_WhenValid()
    {
        var now = DateTime.UtcNow;

        var session = RefreshSession.Create(
            Guid.NewGuid(),
            "old-hash",
            now.AddDays(-1),
            now.AddDays(10),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        session.Rotate("new-hash", now.AddDays(20), now);

        session.TokenHash.Should().Be("new-hash");
        session.ExpiresAtUtc.Should().Be(now.AddDays(20));
        session.LastUsedAtUtc.Should().Be(now);
    }

    [Fact]
    public void Rotate_ShouldThrowValidationException_WhenRevoked()
    {
        var now = DateTime.UtcNow;

        var session = RefreshSession.Create(
            Guid.NewGuid(),
            "old-hash",
            now.AddDays(-1),
            now.AddDays(10),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        session.Revoke("logout", now);

        var act = () => session.Rotate("new-hash", now.AddDays(20), now);

        act.Should().Throw<ValidationException>()
            .WithMessage("Session is revoked.");
    }

    [Fact]
    public void Rotate_ShouldThrowValidationException_WhenNewHashEmpty()
    {
        var now = DateTime.UtcNow;

        var session = RefreshSession.Create(
            Guid.NewGuid(),
            "old-hash",
            now.AddDays(-1),
            now.AddDays(10),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        var act = () => session.Rotate("", now.AddDays(20), now);

        act.Should().Throw<ValidationException>()
            .WithMessage("New token hash is required.");
    }

    [Fact]
    public void Rotate_ShouldThrowValidationException_WhenNewExpiryInvalid()
    {
        var now = DateTime.UtcNow;

        var session = RefreshSession.Create(
            Guid.NewGuid(),
            "old-hash",
            now.AddDays(-1),
            now.AddDays(10),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        var act = () => session.Rotate("new-hash", now, now);

        act.Should().Throw<ValidationException>()
            .WithMessage("Refresh token expiry is invalid.");
    }

    [Fact]
    public void Revoke_ShouldSetRevokedFields_WhenNotRevoked()
    {
        var now = DateTime.UtcNow;

        var session = RefreshSession.Create(
            Guid.NewGuid(),
            "hash",
            now.AddDays(-1),
            now.AddDays(10),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        session.Revoke(" logout ", now);

        session.IsRevoked.Should().BeTrue();
        session.RevokedAtUtc.Should().Be(now);
        session.RevokeReason.Should().Be("logout");
    }

    [Fact]
    public void Revoke_ShouldBeIdempotent_WhenAlreadyRevoked()
    {
        var now = DateTime.UtcNow;

        var session = RefreshSession.Create(
            Guid.NewGuid(),
            "hash",
            now.AddDays(-1),
            now.AddDays(10),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        session.Revoke("first", now);
        var firstRevokedAt = session.RevokedAtUtc;
        var firstReason = session.RevokeReason;

        session.Revoke("second", now.AddMinutes(1));

        session.RevokedAtUtc.Should().Be(firstRevokedAt);
        session.RevokeReason.Should().Be(firstReason);
    }
}