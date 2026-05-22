using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Sessions.Revoke;
using InMoment.Domain.Common;
using InMoment.Domain.Security;
using Moq;

namespace InMoment.Application.Tests.Sessions.Revoke;

public sealed class RevokeSessionHandlerTests
{
    private readonly Mock<IRefreshSessionRepository> _sessions = new();
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private RevokeSessionHandler Create()
        => new(
            _sessions.Object,
            _current.Object,
            _uow.Object);

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUnauthorized()
    {
        _current.Setup(x => x.UserId).Returns(Guid.Empty);

        var handler = Create();

        var act = () => handler.Handle(new RevokeSessionCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Unauthorized.");
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenSessionNotFound()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        _current.Setup(x => x.UserId).Returns(userId);
        _sessions.Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshSession?)null);

        var handler = Create();

        var act = () => handler.Handle(new RevokeSessionCommand(sessionId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Session not found.");
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenSessionBelongsToAnotherUser()
    {
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var session = RefreshSession.Create(
            otherUserId,
            "hash",
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

        _current.Setup(x => x.UserId).Returns(currentUserId);
        _sessions.Setup(x => x.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = Create();

        var act = () => handler.Handle(new RevokeSessionCommand(session.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You cannot revoke another user's session.");
    }

    [Fact]
    public async Task Handle_ShouldRevokeSession_WhenOwnedByCurrentUser()
    {
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var session = RefreshSession.Create(
            userId,
            "hash",
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

        _current.Setup(x => x.UserId).Returns(userId);
        _sessions.Setup(x => x.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = Create();

        await handler.Handle(new RevokeSessionCommand(session.Id), CancellationToken.None);

        session.IsRevoked.Should().BeTrue();
        session.RevokeReason.Should().Be("manual_revoke");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldStayIdempotent_WhenSessionAlreadyRevoked()
    {
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var session = RefreshSession.Create(
            userId,
            "hash",
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

        _current.Setup(x => x.UserId).Returns(userId);
        _sessions.Setup(x => x.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = Create();

        await handler.Handle(new RevokeSessionCommand(session.Id), CancellationToken.None);

        session.IsRevoked.Should().BeTrue();
        session.RevokeReason.Should().Be("manual");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldStayIdempotent_WhenSessionAlreadyExpired()
    {
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var session = RefreshSession.Create(
            userId,
            "hash",
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

        _current.Setup(x => x.UserId).Returns(userId);
        _sessions.Setup(x => x.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = Create();

        await handler.Handle(new RevokeSessionCommand(session.Id), CancellationToken.None);

        session.IsRevoked.Should().BeTrue();
        session.RevokeReason.Should().Be("manual_revoke");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}