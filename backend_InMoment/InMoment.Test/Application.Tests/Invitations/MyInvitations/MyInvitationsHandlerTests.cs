using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Invitations.MyInvitations;
using InMoment.Domain.Groups;

namespace InMoment.Application.Tests.Invitations.MyInvitations;

public sealed class MyInvitationsHandlerTests
{
    private readonly Mock<IInvitationRepository> _invitations = new();
    private readonly Mock<ICurrentUser> _current = new();

    private MyInvitationsHandler Create()
        => new(_invitations.Object, _current.Object);

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenUserHasNoPendingInvitations()
    {
        var currentUserId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _invitations.Setup(x => x.GetPendingByInvitedUserIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GroupInvitation>());

        var handler = Create();

        var result = await handler.Handle(new MyInvitationsQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnPendingInvitations_OrderedByCreatedAtDescending()
    {
        var currentUserId = Guid.NewGuid();
        var inviterA = Guid.NewGuid();
        var inviterB = Guid.NewGuid();

        var older = GroupInvitation.Create(Guid.NewGuid(), currentUserId, inviterA);
        await Task.Delay(5);
        var newer = GroupInvitation.Create(Guid.NewGuid(), currentUserId, inviterB);

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _invitations.Setup(x => x.GetPendingByInvitedUserIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { older, newer });

        var handler = Create();

        var result = await handler.Handle(new MyInvitationsQuery(), CancellationToken.None);

        result.Should().HaveCount(2);

        result[0].Id.Should().Be(newer.Id);
        result[0].GroupId.Should().Be(newer.GroupId);
        result[0].InvitedByUserId.Should().Be(newer.InvitedByUserId);
        result[0].CreatedAt.Should().Be(newer.CreatedAt);

        result[1].Id.Should().Be(older.Id);
        result[1].GroupId.Should().Be(older.GroupId);
        result[1].InvitedByUserId.Should().Be(older.InvitedByUserId);
        result[1].CreatedAt.Should().Be(older.CreatedAt);
    }
}