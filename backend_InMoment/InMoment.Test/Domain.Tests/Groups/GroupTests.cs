using InMoment.Domain.Common;
using InMoment.Domain.Groups;

namespace InMoment.Domain.Tests.Groups;

public sealed class GroupTests
{
    [Fact]
    public void Create_ShouldCreateActiveGroup_WithOwnerMembership()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        const string name = "Family";

        // Act
        var group = Group.Create(name, ownerUserId);

        // Assert
        group.Should().NotBeNull();
        group.Name.Should().Be(name);
        group.OwnerId.Should().Be(ownerUserId);
        group.IsActive.Should().BeTrue();

        group.Members.Should().ContainSingle(m =>
            m.UserId == ownerUserId &&
            m.Role == GroupRole.Owner &&
            m.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldThrowValidationException_WhenNameIsEmpty(string invalidName)
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();

        // Act
        var act = () => Group.Create(invalidName, ownerUserId);

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Create_ShouldThrowValidationException_WhenOwnerUserIdIsEmpty()
    {
        // Arrange
        var ownerUserId = Guid.Empty;

        // Act
        var act = () => Group.Create("Family", ownerUserId);

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void AddMember_ShouldAddActiveMember_WhenUserNotInGroup()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var newUserId = Guid.NewGuid();
        var group = Group.Create("Family", ownerUserId);

        // Act
        group.AddMember(newUserId);

        // Assert
        group.Members.Should().Contain(m =>
            m.UserId == newUserId &&
            m.Role == GroupRole.Member &&
            m.IsActive);
    }

    [Fact]
    public void AddMember_ShouldNotDuplicate_WhenActiveMemberAlreadyExists()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var memberUserId = Guid.NewGuid();
        var group = Group.Create("Family", ownerUserId);

        group.AddMember(memberUserId);

        // Act
        group.AddMember(memberUserId);

        // Assert
        group.Members.Count(m => m.UserId == memberUserId && m.IsActive)
            .Should().Be(1);
    }

    [Fact]
    public void AddMember_ShouldThrowValidationException_WhenUserIdIsEmpty()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var group = Group.Create("Family", ownerUserId);

        // Act
        var act = () => group.AddMember(Guid.Empty);

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void PromoteToAdmin_ShouldPromoteMember_WhenCalledByOwner()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var memberUserId = Guid.NewGuid();
        var group = Group.Create("Family", ownerUserId);

        group.AddMember(memberUserId);

        // Act
        group.PromoteToAdmin(ownerUserId, memberUserId);

        // Assert
        group.Members.Should().Contain(m =>
            m.UserId == memberUserId &&
            m.Role == GroupRole.Admin &&
            m.IsActive);
    }

    [Fact]
    public void PromoteToAdmin_ShouldThrowForbiddenException_WhenCallerIsNotOwner()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var callerUserId = Guid.NewGuid();
        var memberUserId = Guid.NewGuid();
        var group = Group.Create("Family", ownerUserId);

        group.AddMember(callerUserId);
        group.AddMember(memberUserId);

        // Act
        var act = () => group.PromoteToAdmin(callerUserId, memberUserId);

        // Assert
        act.Should().Throw<ForbiddenException>();
    }

    [Fact]
    public void PromoteToAdmin_ShouldThrowValidationException_WhenTargetIsOwner()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var group = Group.Create("Family", ownerUserId);

        // Act
        var act = () => group.PromoteToAdmin(ownerUserId, ownerUserId);

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void PromoteToAdmin_ShouldThrowValidationException_WhenTargetIsNotActiveMember()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var missingUserId = Guid.NewGuid();
        var group = Group.Create("Family", ownerUserId);

        // Act
        var act = () => group.PromoteToAdmin(ownerUserId, missingUserId);

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void DemoteAdmin_ShouldDemoteAdmin_WhenCalledByOwner()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var group = Group.Create("Family", ownerUserId);

        group.AddMember(adminUserId);
        group.PromoteToAdmin(ownerUserId, adminUserId);

        // Act
        group.DemoteAdmin(ownerUserId, adminUserId);

        // Assert
        group.Members.Should().Contain(m =>
            m.UserId == adminUserId &&
            m.Role == GroupRole.Member &&
            m.IsActive);
    }

    [Fact]
    public void DemoteAdmin_ShouldThrowForbiddenException_WhenCallerIsNotOwner()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var callerUserId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var group = Group.Create("Family", ownerUserId);

        group.AddMember(callerUserId);
        group.AddMember(adminUserId);
        group.PromoteToAdmin(ownerUserId, adminUserId);

        // Act
        var act = () => group.DemoteAdmin(callerUserId, adminUserId);

        // Assert
        act.Should().Throw<ForbiddenException>();
    }

    [Fact]
    public void DemoteAdmin_ShouldThrowValidationException_WhenTargetIsOwner()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var group = Group.Create("Family", ownerUserId);

        // Act
        var act = () => group.DemoteAdmin(ownerUserId, ownerUserId);

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void DemoteAdmin_ShouldThrowValidationException_WhenTargetIsNotActiveMember()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var missingUserId = Guid.NewGuid();
        var group = Group.Create("Family", ownerUserId);

        // Act
        var act = () => group.DemoteAdmin(ownerUserId, missingUserId);

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void TransferOwnership_ShouldTransferOwnerRole_ToActiveMember()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var newOwnerUserId = Guid.NewGuid();
        var group = Group.Create("Family", ownerUserId);

        group.AddMember(newOwnerUserId);

        // Act
        group.TransferOwnership(ownerUserId, newOwnerUserId);

        // Assert
        group.OwnerId.Should().Be(newOwnerUserId);

        group.Members.Should().Contain(m =>
            m.UserId == newOwnerUserId &&
            m.Role == GroupRole.Owner &&
            m.IsActive);

        group.Members.Should().Contain(m =>
            m.UserId == ownerUserId &&
            m.Role == GroupRole.Admin &&
            m.IsActive);
    }

    [Fact]
    public void TransferOwnership_ShouldThrowForbiddenException_WhenCallerIsNotOwner()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var callerUserId = Guid.NewGuid();
        var newOwnerUserId = Guid.NewGuid();
        var group = Group.Create("Family", ownerUserId);

        group.AddMember(callerUserId);
        group.AddMember(newOwnerUserId);

        // Act
        var act = () => group.TransferOwnership(callerUserId, newOwnerUserId);

        // Assert
        act.Should().Throw<ForbiddenException>();
    }

    [Fact]
    public void TransferOwnership_ShouldThrowValidationException_WhenNewOwnerIsNotActiveMember()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var missingUserId = Guid.NewGuid();
        var group = Group.Create("Family", ownerUserId);

        // Act
        var act = () => group.TransferOwnership(ownerUserId, missingUserId);

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void TransferOwnership_ShouldThrowValidationException_WhenNewOwnerEqualsCurrentOwner()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var group = Group.Create("Family", ownerUserId);

        // Act
        var act = () => group.TransferOwnership(ownerUserId, ownerUserId);

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Leave_ShouldDeactivateMembership_WhenRegularMemberLeaves()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var memberUserId = Guid.NewGuid();
        var group = Group.Create("Family", ownerUserId);

        group.AddMember(memberUserId);

        // Act
        group.Leave(memberUserId);

        // Assert
        group.Members.Should().Contain(m =>
            m.UserId == memberUserId &&
            !m.IsActive);
    }

    [Fact]
    public void Leave_ShouldThrowValidationException_WhenOwnerLeavesWhileOtherActiveMembersExist()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var memberUserId = Guid.NewGuid();
        var group = Group.Create("Family", ownerUserId);

        group.AddMember(memberUserId);

        // Act
        var act = () => group.Leave(ownerUserId);

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Leave_ShouldDeactivateGroup_WhenLastOwnerLeavesAndNoMembersRemain()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var group = Group.Create("Family", ownerUserId);

        // Act
        group.Leave(ownerUserId);

        // Assert
        group.IsActive.Should().BeFalse();
        group.Members.Should().Contain(m =>
            m.UserId == ownerUserId &&
            !m.IsActive);
    }
}