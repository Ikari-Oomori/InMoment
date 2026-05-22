using FluentAssertions;
using InMoment.Application.Abstractions.Communication;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Contacts.Invites.Send;
using InMoment.Domain.Common;
using InMoment.Domain.Contacts;
using InMoment.Domain.Users;
using Moq;

namespace InMoment.Application.Tests.Contacts.Invites;

public sealed class SendContactInviteHandlerTests
{
    private readonly Mock<IContactInviteRepository> _invites = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IContactInviteSender> _sender = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private SendContactInviteHandler Create()
        => new(_invites.Object, _users.Object, _current.Object, _sender.Object, _uow.Object);

    [Fact]
    public async Task Handle_ShouldThrowForbidden_WhenUnauthorized()
    {
        _current.SetupGet(x => x.UserId).Returns(Guid.Empty);

        var handler = Create();

        var act = () => handler.Handle(
            new SendContactInviteCommand("test@example.com", null, "Test"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_ShouldCreateEmailInvite()
    {
        var userId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(userId);
        _users.Setup(x => x.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _invites.Setup(x => x.GetPendingByEmailAsync(userId, "test@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContactInvite?)null);

        ContactInvite? added = null;
        _invites.Setup(x => x.AddAsync(It.IsAny<ContactInvite>(), It.IsAny<CancellationToken>()))
            .Callback<ContactInvite, CancellationToken>((x, _) => added = x)
            .Returns(Task.CompletedTask);

        var handler = Create();

        var result = await handler.Handle(
            new SendContactInviteCommand("test@example.com", null, "Test"),
            CancellationToken.None);

        result.Channel.Should().Be(ContactInviteChannel.Email);
        result.Email.Should().Be("test@example.com");
        added.Should().NotBeNull();

        _sender.Verify(x => x.SendAsync(It.IsAny<ContactInviteSendRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}