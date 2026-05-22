using InMoment.Domain.Groups;

namespace InMoment.Tests.Domain.Tests.Groups;

public sealed class GroupMemberTests
{
    [Fact]
    public void CreateOwner_ShouldInitializeOwnerMember()
    {
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var member = GroupMember.CreateOwner(groupId, userId);

        member.GroupId.Should().Be(groupId);
        member.UserId.Should().Be(userId);
        member.Role.Should().Be(GroupRole.Owner);
        member.IsActive.Should().BeTrue();
        member.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CreateAdmin_ShouldInitializeAdminMember()
    {
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var member = GroupMember.CreateAdmin(groupId, userId);

        member.GroupId.Should().Be(groupId);
        member.UserId.Should().Be(userId);
        member.Role.Should().Be(GroupRole.Admin);
        member.IsActive.Should().BeTrue();
        member.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CreateMember_ShouldInitializeRegularMember()
    {
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var member = GroupMember.CreateMember(groupId, userId);

        member.GroupId.Should().Be(groupId);
        member.UserId.Should().Be(userId);
        member.Role.Should().Be(GroupRole.Member);
        member.IsActive.Should().BeTrue();
        member.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void SetRole_ShouldUpdateRole()
    {
        var member = GroupMember.CreateMember(Guid.NewGuid(), Guid.NewGuid());

        member.SetRole(GroupRole.Admin);

        member.Role.Should().Be(GroupRole.Admin);
    }

    [Fact]
    public void Deactivate_ShouldSetInactive()
    {
        var member = GroupMember.CreateMember(Guid.NewGuid(), Guid.NewGuid());

        member.Deactivate();

        member.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_ShouldRemainInactive_WhenCalledTwice()
    {
        var member = GroupMember.CreateMember(Guid.NewGuid(), Guid.NewGuid());
        member.Deactivate();

        var act = () => member.Deactivate();

        act.Should().NotThrow();
        member.IsActive.Should().BeFalse();
    }
}