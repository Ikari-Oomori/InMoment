using System.Reflection;
using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Groups.GetMembers;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;
using InMoment.Domain.Users;
using Moq;

namespace InMoment.Application.Tests.Groups.GetMembers;

public sealed class GetGroupMembersHandlerTests
{
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ICurrentUser> _current = new();

    private GetGroupMembersHandler Create()
        => new(_groups.Object, _users.Object, _current.Object);

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenGroupNotFound()
    {
        var groupId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(currentUserId);
        _groups.Setup(x => x.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group?)null);

        var handler = Create();

        var act = () => handler.Handle(
            new GetGroupMembersQuery(groupId),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Group not found.");

        _users.Verify(x => x.GetByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenCurrentUserIsNotMember()
    {
        var ownerId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();

        var group = Group.Create("Team", ownerId);

        _current.SetupGet(x => x.UserId).Returns(outsiderId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = Create();

        var act = () => handler.Handle(
            new GetGroupMembersQuery(group.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("You are not a member of this group.");

        _users.Verify(x => x.GetByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnOnlyActiveMembers_OrderedByRoleThenUserName()
    {
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var memberAId = Guid.NewGuid();
        var memberBId = Guid.NewGuid();
        var removedMemberId = Guid.NewGuid();

        var group = Group.Create("Team", ownerId);
        group.AddMember(adminId);
        group.AddMember(memberAId);
        group.AddMember(memberBId);
        group.AddMember(removedMemberId);

        group.PromoteToAdmin(ownerId, adminId);
        group.RemoveMember(ownerId, removedMemberId);

        var ownerUser = CreateUserWithId(ownerId, "owner@test.com", "owner_user", "Owner", "One");
        var adminUser = CreateUserWithId(adminId, "admin@test.com", "admin_user", "Admin", "One");
        var memberBUser = CreateUserWithId(memberBId, "memberb@test.com", "beta_user", "Beta", "User");
        var memberAUser = CreateUserWithId(memberAId, "membera@test.com", "alpha_user", "Alpha", "User");

        _current.SetupGet(x => x.UserId).Returns(ownerId);
        _groups.Setup(x => x.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _users.Setup(x => x.GetByIdsAsync(
                It.Is<IReadOnlyList<Guid>>(ids =>
                    ids.Count == 4 &&
                    ids.Contains(ownerId) &&
                    ids.Contains(adminId) &&
                    ids.Contains(memberAId) &&
                    ids.Contains(memberBId) &&
                    !ids.Contains(removedMemberId)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { memberBUser, adminUser, ownerUser, memberAUser });

        var handler = Create();

        var result = await handler.Handle(
            new GetGroupMembersQuery(group.Id),
            CancellationToken.None);

        result.Should().HaveCount(4);

        result[0].UserId.Should().Be(ownerId);
        result[0].UserName.Should().Be("owner_user");
        result[0].Role.Should().Be(GroupRole.Owner);
        result[0].IsOwner.Should().BeTrue();
        result[0].IsAdmin.Should().BeFalse();

        result[1].UserId.Should().Be(adminId);
        result[1].UserName.Should().Be("admin_user");
        result[1].Role.Should().Be(GroupRole.Admin);
        result[1].IsOwner.Should().BeFalse();
        result[1].IsAdmin.Should().BeTrue();

        result[2].UserId.Should().Be(memberAId);
        result[2].UserName.Should().Be("alpha_user");
        result[2].Role.Should().Be(GroupRole.Member);
        result[2].IsOwner.Should().BeFalse();
        result[2].IsAdmin.Should().BeFalse();

        result[3].UserId.Should().Be(memberBId);
        result[3].UserName.Should().Be("beta_user");
        result[3].Role.Should().Be(GroupRole.Member);
        result[3].IsOwner.Should().BeFalse();
        result[3].IsAdmin.Should().BeFalse();
    }

    private static User CreateUserWithId(
        Guid id,
        string email,
        string userName,
        string firstName,
        string lastName)
    {
        var user = User.Create(email, "hash", userName, firstName, lastName);

        var idProperty = typeof(User).GetProperty(
            "Id",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        idProperty.Should().NotBeNull("User entity must expose Id property.");

        idProperty!.SetValue(user, id);
        return user;
    }
}