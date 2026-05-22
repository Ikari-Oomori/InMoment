using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Sessions.List;
using InMoment.Domain.Common;
using InMoment.Domain.Security;

namespace InMoment.Application.Tests.Sessions.List;

public sealed class ListSessionsHandlerTests
{
    private readonly Mock<IRefreshSessionRepository> _sessions = new();
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IRefreshTokenService> _refreshTokens = new();

    private ListSessionsHandler Create()
        => new(
            _sessions.Object,
            _current.Object,
            _refreshTokens.Object);

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenCurrentUserIsEmpty()
    {
        _current.SetupGet(x => x.UserId).Returns(Guid.Empty);

        var handler = Create();

        var act = () => handler.Handle(new ListSessionsQuery(null), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Unauthorized.");

        _sessions.Verify(x => x.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnSessions_AndMarkCurrent_WhenRefreshTokenProvided()
    {
        var currentUserId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var session1 = RefreshSession.Create(
            currentUserId,
            "hash-current",
            now.AddDays(-3),
            now.AddDays(7),
            "iPhone",
            "iOS",
            "10.0.0.1",
            "Safari",
            null,
            null,
            null,
            null);

        var session2 = RefreshSession.Create(
            currentUserId,
            "hash-other",
            now.AddDays(-2),
            now.AddDays(5),
            "Web",
            "Browser",
            "10.0.0.2",
            "Chrome",
            null,
            null,
            null,
            null);

        session2.Revoke("manual", now);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _refreshTokens.Setup(x => x.HashToken("raw-refresh-token"))
            .Returns("hash-current");

        _sessions.Setup(x => x.GetByUserIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RefreshSession> { session1, session2 });

        var handler = Create();

        var result = await handler.Handle(
            new ListSessionsQuery("raw-refresh-token"),
            CancellationToken.None);

        result.Should().HaveCount(2);

        result.Should().ContainSingle(x =>
            x.Id == session1.Id &&
            x.IsCurrent &&
            !x.IsRevoked &&
            x.DeviceName == "iPhone" &&
            x.Platform == "iOS");

        result.Should().ContainSingle(x =>
            x.Id == session2.Id &&
            !x.IsCurrent &&
            x.IsRevoked &&
            x.DeviceName == "Web" &&
            x.Platform == "Browser");

        _refreshTokens.Verify(x => x.HashToken("raw-refresh-token"), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnSessions_WithoutCurrent_WhenRefreshTokenNotProvided()
    {
        var currentUserId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var session = RefreshSession.Create(
            currentUserId,
            "hash-1",
            now.AddDays(-1),
            now.AddDays(10),
            "Laptop",
            "Windows",
            "127.0.0.1",
            "Edge",
            null,
            null,
            null,
            null);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _sessions.Setup(x => x.GetByUserIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RefreshSession> { session });

        var handler = Create();

        var result = await handler.Handle(
            new ListSessionsQuery(null),
            CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(session.Id);
        result[0].IsCurrent.Should().BeFalse();
        result[0].IsRevoked.Should().BeFalse();

        _refreshTokens.Verify(x => x.HashToken(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldIgnoreWhitespaceRefreshToken()
    {
        var currentUserId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _sessions.Setup(x => x.GetByUserIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RefreshSession>());

        var handler = Create();

        var result = await handler.Handle(
            new ListSessionsQuery("   "),
            CancellationToken.None);

        result.Should().BeEmpty();

        _refreshTokens.Verify(x => x.HashToken(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnSessions_WithoutCurrent_WhenRefreshTokenHashDoesNotMatchAnySession()
    {
        var currentUserId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var session1 = RefreshSession.Create(
            currentUserId,
            "hash-1",
            now.AddDays(-2),
            now.AddDays(8),
            "Phone",
            "iOS",
            null,
            null,
            null,
            null,
            null,
            null);

        var session2 = RefreshSession.Create(
            currentUserId,
            "hash-2",
            now.AddDays(-1),
            now.AddDays(6),
            "Laptop",
            "Windows",
            null,
            null,
            null,
            null,
            null,
            null);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _refreshTokens.Setup(x => x.HashToken("stale-raw-token"))
            .Returns("hash-missing");

        _sessions.Setup(x => x.GetByUserIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RefreshSession> { session1, session2 });

        var handler = Create();

        var result = await handler.Handle(
            new ListSessionsQuery("stale-raw-token"),
            CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(x => x.IsCurrent == false);
    }

    [Fact]
    public async Task Handle_ShouldKeepExpiredAndRevokedSessionsInHistory()
    {
        var currentUserId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var active = RefreshSession.Create(
            currentUserId,
            "hash-active",
            now.AddDays(-1),
            now.AddDays(10),
            "Phone",
            "iOS",
            null,
            null,
            null,
            null,
            null,
            null);

        var expired = RefreshSession.Create(
            currentUserId,
            "hash-expired",
            now.AddDays(-10),
            now.AddMinutes(-1),
            "Old Web",
            "Browser",
            null,
            null,
            null,
            null,
            null,
            null);

        var revoked = RefreshSession.Create(
            currentUserId,
            "hash-revoked",
            now.AddDays(-5),
            now.AddDays(5),
            "Tablet",
            "Android",
            null,
            null,
            null,
            null,
            null,
            null);
        revoked.Revoke("manual", now);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _sessions.Setup(x => x.GetByUserIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RefreshSession> { active, expired, revoked });

        var handler = Create();

        var result = await handler.Handle(
            new ListSessionsQuery(null),
            CancellationToken.None);

        result.Should().HaveCount(3);
        result.Should().Contain(x => x.Id == active.Id && x.IsRevoked == false);
        result.Should().Contain(x => x.Id == expired.Id && x.IsRevoked == false);
        result.Should().Contain(x => x.Id == revoked.Id && x.IsRevoked == true);
    }
}