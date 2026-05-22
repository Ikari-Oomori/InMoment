using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Auth.Logout;
using InMoment.Domain.Security;
using InMoment.Domain.Users;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using System.Net;
using System.Runtime.InteropServices;

namespace InMoment.Application.Tests.Auth.Logout;

public sealed class LogoutHandlerTests
{
    private readonly Mock<IRefreshSessionRepository> _sessions = new();
    private readonly Mock<IRefreshTokenService> _refreshTokens = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private LogoutHandler Create()
        => new(
            _sessions.Object,
            _refreshTokens.Object,
            _uow.Object);

    [Fact]
    public async Task Handle_ShouldReturn_WhenRefreshTokenEmpty()
    {
        var handler = Create();

        await handler.Handle(new LogoutCommand(""), CancellationToken.None);

        _refreshTokens.Verify(x => x.HashToken(It.IsAny<string>()), Times.Never);
        _sessions.Verify(x => x.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturn_WhenSessionNotFound()
    {
        var rawToken = "raw-refresh-token";
        var hash = "hash";

        _refreshTokens.Setup(x => x.HashToken(rawToken)).Returns(hash);
        _sessions.Setup(x => x.GetByTokenHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshSession?)null);

        var handler = Create();

        await handler.Handle(new LogoutCommand(rawToken), CancellationToken.None);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRevokeSession_WhenSessionExists()
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

        _refreshTokens.Setup(x => x.HashToken(rawToken)).Returns(hash);
        _sessions.Setup(x => x.GetByTokenHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = Create();

        await handler.Handle(new LogoutCommand(rawToken), CancellationToken.None);

        session.IsRevoked.Should().BeTrue();
        session.RevokeReason.Should().Be("logout");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldKeepIdempotency_WhenSessionAlreadyRevoked()
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

        _refreshTokens.Setup(x => x.HashToken(rawToken)).Returns(hash);
        _sessions.Setup(x => x.GetByTokenHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = Create();

        await handler.Handle(new LogoutCommand(rawToken), CancellationToken.None);

        session.IsRevoked.Should().BeTrue();
        session.RevokeReason.Should().Be("manual");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}