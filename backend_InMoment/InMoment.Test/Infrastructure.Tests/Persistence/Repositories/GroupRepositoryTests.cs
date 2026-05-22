using FluentAssertions;
using InMoment.Domain.Groups;
using InMoment.Domain.Users;
using InMoment.Infrastructure.Persistence.Repositories;
using InMoment.Test.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Tests.Persistence.Repositories;

public sealed class GroupRepositoryTests
{
    [Fact]
    public async Task AddAsync_ShouldPersistGroup()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;
        var repo = new GroupRepository(db);

        var ownerId = Guid.NewGuid();
        var group = Group.Create("Test Group", ownerId);

        await repo.AddAsync(group, CancellationToken.None);
        await db.SaveChangesAsync();

        var saved = await db.Groups.FindAsync(group.Id);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Test Group");
        saved.OwnerId.Should().Be(ownerId);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnActiveGroup_WithMembersIncluded()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var group = Group.Create("Group 1", ownerId);
        group.AddMember(memberId);

        db.Groups.Add(group);
        await db.SaveChangesAsync();

        var repo = new GroupRepository(db);

        var result = await repo.GetByIdAsync(group.Id, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(group.Id);
        result.Members.Should().Contain(x => x.UserId == ownerId && x.IsActive);
        result.Members.Should().Contain(x => x.UserId == memberId && x.IsActive);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenGroupInactive()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var ownerId = Guid.NewGuid();

        var group = Group.Create("Group 1", ownerId);
        group.Leave(ownerId);

        db.Groups.Add(group);
        await db.SaveChangesAsync();

        var repo = new GroupRepository(db);

        var result = await repo.GetByIdAsync(group.Id, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenMissing()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var repo = new GroupRepository(db);

        var result = await repo.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserIdAsync_ShouldReturnOnlyActiveGroupsWhereUserIsActiveMember_OrderedByCreatedAtDescending()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var oldest = Group.Create("Oldest", userId);
        var newest = Group.Create("Newest", otherUserId);
        newest.AddMember(userId);

        var inactiveMembershipGroup = Group.Create("Inactive Membership", otherUserId);
        inactiveMembershipGroup.AddMember(userId);
        inactiveMembershipGroup.Leave(userId);

        var inactiveGroup = Group.Create("Inactive Group", userId);
        inactiveGroup.Leave(userId);

        SetCreatedAt(oldest, new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(newest, new DateTime(2026, 1, 3, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(inactiveMembershipGroup, new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(inactiveGroup, new DateTime(2026, 1, 4, 10, 0, 0, DateTimeKind.Utc));

        db.Groups.AddRange(oldest, newest, inactiveMembershipGroup, inactiveGroup);
        await db.SaveChangesAsync();

        var repo = new GroupRepository(db);

        var result = await repo.GetByUserIdAsync(userId, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(x => x.Id).Should().ContainInOrder(newest.Id, oldest.Id);
        result.Should().OnlyContain(x => x.IsActive);
    }

    [Fact]
    public async Task GetByUserIdAsync_ShouldReturnEmpty_WhenUserHasNoGroups()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var repo = new GroupRepository(db);

        var result = await repo.GetByUserIdAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task IsMemberAsync_ShouldReturnTrue_WhenUserIsActiveMember()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var ownerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var group = Group.Create("Group 1", ownerId);
        group.AddMember(userId);

        db.Groups.Add(group);
        await db.SaveChangesAsync();

        var repo = new GroupRepository(db);

        var result = await repo.IsMemberAsync(group.Id, userId, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsMemberAsync_ShouldReturnFalse_WhenMembershipInactive()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var ownerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var group = Group.Create("Group 1", ownerId);
        group.AddMember(userId);
        group.Leave(userId);

        db.Groups.Add(group);
        await db.SaveChangesAsync();

        var repo = new GroupRepository(db);

        var result = await repo.IsMemberAsync(group.Id, userId, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddMemberAsync_ShouldPersistMember()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;
        var repo = new GroupRepository(db);

        var owner = User.Create(
            "owner-addmember@test.com",
            "hash",
            "owner_addmember",
            "Owner",
            "AddMember");

        var user = User.Create(
            "member-addmember@test.com",
            "hash",
            "member_addmember",
            "Member",
            "AddMember");

        var group = Group.Create("Test Group", owner.Id);

        db.Users.AddRange(owner, user);
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        var member = GroupMember.CreateMember(group.Id, user.Id);

        await repo.AddMemberAsync(member, CancellationToken.None);
        await db.SaveChangesAsync();

        var saved = await db.GroupMembers.FirstOrDefaultAsync(x => x.Id == member.Id);
        saved.Should().NotBeNull();
        saved!.GroupId.Should().Be(group.Id);
        saved.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task SearchMyGroupsAsync_ShouldReturnOnlyMatchingGroupsWhereUserIsActiveMember_OrderedByName()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var alpha = Group.Create("Alpha Team", userId);
        var beta = Group.Create("Beta Team", userId);
        var gamma = Group.Create("Gamma Team", otherUserId);
        gamma.AddMember(userId);

        var hidden = Group.Create("Alpha Hidden", otherUserId);
        hidden.AddMember(userId);
        hidden.Leave(userId);

        db.Groups.AddRange(alpha, beta, gamma, hidden);
        await db.SaveChangesAsync();

        var repo = new GroupRepository(db);

        var result = await repo.SearchMyGroupsAsync(userId, "team", 10, CancellationToken.None);

        result.Should().HaveCount(3);
        result.Select(x => x.Name).Should().ContainInOrder("Alpha Team", "Beta Team", "Gamma Team");
        result.Should().NotContain(x => x.Name == "Alpha Hidden");
    }

    [Fact]
    public async Task SearchMyGroupsAsync_ShouldBeCaseInsensitive_AndRespectLimit()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var userId = Guid.NewGuid();

        var a = Group.Create("alpha one", userId);
        var b = Group.Create("Alpha two", userId);
        var c = Group.Create("ALPHA three", userId);

        db.Groups.AddRange(a, b, c);
        await db.SaveChangesAsync();

        var repo = new GroupRepository(db);

        var result = await repo.SearchMyGroupsAsync(userId, " AlPhA ", 2, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(x => x.Name).Should().OnlyContain(x => x.ToLower().Contains("alpha"));
        result.Select(x => x.Name).Should().OnlyHaveUniqueItems();
    }

    private static void SetCreatedAt(Group group, DateTime createdAtUtc)
    {
        typeof(Group)
            .GetProperty(nameof(Group.CreatedAt))!
            .SetValue(group, createdAtUtc);
    }
}