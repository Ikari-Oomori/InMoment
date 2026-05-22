using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Contacts.Invites.Cancel;
using InMoment.Domain.Common;
using InMoment.Domain.Contacts;
using Moq;

namespace InMoment.Application.Tests.Contacts.Invites;

public sealed class CancelContactInviteHandlerTests
{
    private readonly Mock<IContactInviteRepository> _invites = new();
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private CancelContactInviteHandler Create()
        => new(_invites.Object, _current.Object, _uow.Object);

    [Fact]
    public async Task Handle_ShouldCancelInvite_WhenOwnedByCurrentUser()
    {
        var userId = Guid.NewGuid();
        var invite = ContactInvite.CreateEmail(userId, "test@example.com", "Test", "token");

        _current.SetupGet(x => x.UserId).Returns(userId);
        _invites.Setup(x => x.GetByIdAsync(invite.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invite);

        var handler = Create();

        await handler.Handle(new CancelContactInviteCommand(invite.Id), CancellationToken.None);

        invite.Status.Should().Be(ContactInviteStatus.Cancelled);
        invite.CancelledAtUtc.Should().NotBeNull();

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}