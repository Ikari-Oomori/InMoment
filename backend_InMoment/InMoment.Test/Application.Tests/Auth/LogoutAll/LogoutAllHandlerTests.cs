using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Auth.LogoutAll;
using InMoment.Domain.Common;
using InMoment.Domain.Security;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using System.Net;
using System.Runtime.InteropServices;

namespace InMoment.Application.Tests.Auth.LogoutAll;

public sealed class LogoutAllHandlerTests
{
    private readonly Mock<IRefreshSessionRepository> _sessions = new();
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private LogoutAllHandler Create()
        => new(
            _sessions.Object,
            _current.Object,
            _uow.Object);

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUnauthorized()
    {
        _current.Setup(x => x.UserId).Returns(Guid.Empty);

        var handler = Create();

        var act = () => handler.Handle(new LogoutAllCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Unauthorized.");
    }

    [Fact]
    public async Task Handle_ShouldRevokeOnlyActiveSessions()
    {
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var active1 = RefreshSession.Create(
             userId,
             "hash2",
             now.AddDays(-2),
             now.AddDays(5),
             "device2",
             "android",
             null,
             null,
             null,
             null,
             null,
             null);

        var active2 = RefreshSession.Create(
            userId,
            "hash2",
            now.AddDays(-2),
            now.AddDays(5),
            "device2",
            "android",
            null,
            null,
            null,
            null,
            null,
            null);

        var revoked = RefreshSession.Create(
            userId,
            "hash2",
            now.AddDays(-2),
            now.AddDays(5),
            "device2",
            "android",
            null,
            null,
            null,
            null,
            null,
            null); ;

        var expired = RefreshSession.Create(
            userId,
            "hash2",
            now.AddDays(-10),
            now.AddDays(-1),
            "device2",
            "android",
            null,
            null,
            null,
            null,
            null,
            null);

        _current.Setup(x => x.UserId).Returns(userId);
        _sessions.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { active1, active2, revoked, expired });

        var handler = Create();

        await handler.Handle(new LogoutAllCommand(), CancellationToken.None);

        active1.IsRevoked.Should().BeTrue();
        active1.RevokeReason.Should().Be("logout_all");

        active2.IsRevoked.Should().BeTrue();
        active2.RevokeReason.Should().Be("logout_all");

        revoked.IsRevoked.Should().BeTrue();
        revoked.RevokeReason.Should().Be("logout_all");

        expired.IsRevoked.Should().BeFalse();

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldRevokeCurrentSessionToo_WhenItIsActive()
    {
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var currentSession = RefreshSession.Create(
            userId,
            "hash2",
            now.AddDays(-1),
            now.AddDays(14),
            "device2",
            "android",
            null,
            null,
            null,
            null,
            null,
            null);

        _current.Setup(x => x.UserId).Returns(userId);
        _sessions.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { currentSession });

        var handler = Create();

        await handler.Handle(new LogoutAllCommand(), CancellationToken.None);

        currentSession.IsRevoked.Should().BeTrue();
        currentSession.RevokeReason.Should().Be("logout_all");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldSave_WhenNoSessionsFound()
    {
        var userId = Guid.NewGuid();

        _current.Setup(x => x.UserId).Returns(userId);
        _sessions.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RefreshSession>());

        var handler = Create();

        await handler.Handle(new LogoutAllCommand(), CancellationToken.None);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}