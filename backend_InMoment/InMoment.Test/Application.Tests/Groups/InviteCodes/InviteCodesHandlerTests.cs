using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Groups.InviteCodes.Create;
using InMoment.Application.Features.Groups.InviteCodes.Join;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;

namespace InMoment.Application.Tests.Groups.InviteCodes;

public sealed class InviteCodesHandlerTests
{
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IGroupInviteCodeRepository> _codes = new();
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IAppTransaction> _tx = new();

    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();

    private CreateInviteCodeHandler CreateCreateHandler()
        => new(_groups.Object, _codes.Object, _current.Object, _uow.Object);

    private JoinByCodeHandler CreateJoinHandler()
        => new(_codes.Object, _groups.Object, _current.Object, _uow.Object);

    private static Group CreateGroup(Guid ownerId)
    {
        var group = Group.Create("Test Group", ownerId);
        return group;
    }

    public InviteCodesHandlerTests()
    {
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tx.Object);

        _tx.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task CreateInviteCode_ShouldCreateCode_ForOwner()
    {
        var group = CreateGroup(_currentUserId);

        GroupInviteCode? savedCode = null;

        _groups.Setup(x => x.GetByIdAsync(_groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _codes.Setup(x => x.AddAsync(It.IsAny<GroupInviteCode>(), It.IsAny<CancellationToken>()))
            .Callback<GroupInviteCode, CancellationToken>((c, _) => savedCode = c)
            .Returns(Task.CompletedTask);

        _current.Setup(x => x.UserId).Returns(_currentUserId);

        var handler = CreateCreateHandler();

        var code = await handler.Handle(
            new CreateInviteCodeCommand(_groupId, 5, 24),
            CancellationToken.None);

        code.Should().NotBeNullOrWhiteSpace();
        code.Should().HaveLength(8);
        code.Should().MatchRegex("^[A-Z0-9]{8}$");

        savedCode.Should().NotBeNull();
        savedCode!.GroupId.Should().Be(_groupId);
        savedCode.CreatedByUserId.Should().Be(_currentUserId);
        savedCode.MaxUses.Should().Be(5);
        savedCode.ExpiresAtUtc.Should().NotBeNull();

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateInviteCode_ShouldWork_ForAdmin()
    {
        var ownerId = Guid.NewGuid();
        var group = CreateGroup(ownerId);
        group.AddMember(_currentUserId);
        group.PromoteToAdmin(ownerId, _currentUserId);

        _groups.Setup(x => x.GetByIdAsync(_groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _codes.Setup(x => x.AddAsync(It.IsAny<GroupInviteCode>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _current.Setup(x => x.UserId).Returns(_currentUserId);

        var handler = CreateCreateHandler();

        var code = await handler.Handle(
            new CreateInviteCodeCommand(_groupId, null, null),
            CancellationToken.None);

        code.Should().NotBeNullOrWhiteSpace();

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateInviteCode_ShouldThrow_WhenUserIsNotManager()
    {
        var ownerId = Guid.NewGuid();
        var group = CreateGroup(ownerId);
        group.AddMember(_currentUserId);

        _groups.Setup(x => x.GetByIdAsync(_groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _current.Setup(x => x.UserId).Returns(_currentUserId);

        var handler = CreateCreateHandler();

        var act = () => handler.Handle(
            new CreateInviteCodeCommand(_groupId, null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();

        _codes.Verify(x => x.AddAsync(It.IsAny<GroupInviteCode>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task JoinByCode_ShouldAddMember_AndSave_WhenUserNotMemberYet()
    {
        var ownerId = Guid.NewGuid();
        var group = CreateGroup(ownerId);

        var inviteCode = GroupInviteCode.Create(
            groupId: _groupId,
            code: "ABCDEFGH",
            createdByUserId: ownerId,
            createdAtUtc: DateTime.UtcNow.AddMinutes(-5),
            expiresAtUtc: DateTime.UtcNow.AddHours(1),
            maxUses: 3);

        _codes.Setup(x => x.GetByCodeAsync("ABCDEFGH", It.IsAny<CancellationToken>()))
            .ReturnsAsync(inviteCode);

        _groups.Setup(x => x.GetByIdAsync(_groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _current.Setup(x => x.UserId).Returns(_currentUserId);

        var handler = CreateJoinHandler();

        await handler.Handle(new JoinByCodeCommand(" abcdefgh "), CancellationToken.None);

        group.IsMember(_currentUserId).Should().BeTrue();

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task JoinByCode_ShouldNotDuplicateMembership_WhenUserAlreadyMember()
    {
        var group = CreateGroup(_currentUserId);

        var inviteCode = GroupInviteCode.Create(
            groupId: _groupId,
            code: "ABCDEFGH",
            createdByUserId: _currentUserId,
            createdAtUtc: DateTime.UtcNow.AddMinutes(-5),
            expiresAtUtc: DateTime.UtcNow.AddHours(1),
            maxUses: 5);

        _codes.Setup(x => x.GetByCodeAsync("ABCDEFGH", It.IsAny<CancellationToken>()))
            .ReturnsAsync(inviteCode);

        _groups.Setup(x => x.GetByIdAsync(_groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _current.Setup(x => x.UserId).Returns(_currentUserId);

        var handler = CreateJoinHandler();

        await handler.Handle(new JoinByCodeCommand("ABCDEFGH"), CancellationToken.None);

        group.Members.Count(x => x.IsActive && x.UserId == _currentUserId).Should().Be(1);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);

        _tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task JoinByCode_ShouldThrow_WhenCodeIsEmpty()
    {
        var handler = CreateJoinHandler();

        var act = () => handler.Handle(new JoinByCodeCommand("   "), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Code is required.");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task JoinByCode_ShouldThrow_WhenCodeNotFound()
    {
        _codes.Setup(x => x.GetByCodeAsync("ABCDEFGH", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GroupInviteCode?)null);

        var handler = CreateJoinHandler();

        var act = () => handler.Handle(new JoinByCodeCommand("ABCDEFGH"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Invite code not found.");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task JoinByCode_ShouldThrow_WhenGroupNotFound()
    {
        var inviteCode = GroupInviteCode.Create(
            groupId: _groupId,
            code: "ABCDEFGH",
            createdByUserId: Guid.NewGuid(),
            createdAtUtc: DateTime.UtcNow.AddMinutes(-5),
            expiresAtUtc: DateTime.UtcNow.AddHours(1),
            maxUses: 5);

        _codes.Setup(x => x.GetByCodeAsync("ABCDEFGH", It.IsAny<CancellationToken>()))
            .ReturnsAsync(inviteCode);

        _groups.Setup(x => x.GetByIdAsync(_groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group?)null);

        var handler = CreateJoinHandler();

        var act = () => handler.Handle(new JoinByCodeCommand("ABCDEFGH"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Group not found.");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task JoinByCode_ShouldThrow_WhenGroupInactive()
    {
        var ownerId = Guid.NewGuid();
        var group = CreateGroup(ownerId);
        group.Leave(ownerId);

        var inviteCode = GroupInviteCode.Create(
            groupId: _groupId,
            code: "ABCDEFGH",
            createdByUserId: ownerId,
            createdAtUtc: DateTime.UtcNow.AddMinutes(-5),
            expiresAtUtc: DateTime.UtcNow.AddHours(1),
            maxUses: 5);

        _codes.Setup(x => x.GetByCodeAsync("ABCDEFGH", It.IsAny<CancellationToken>()))
            .ReturnsAsync(inviteCode);

        _groups.Setup(x => x.GetByIdAsync(_groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _current.Setup(x => x.UserId).Returns(_currentUserId);

        var handler = CreateJoinHandler();

        var act = () => handler.Handle(new JoinByCodeCommand("ABCDEFGH"), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Group is inactive.");

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}