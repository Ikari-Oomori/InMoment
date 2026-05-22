using FluentAssertions;
using InMoment.Domain.Groups;
using InMoment.Domain.Users;
using InMoment.Infrastructure.Persistence.Repositories;
using InMoment.Test.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Tests.Persistence.Repositories;

public sealed class InvitationRepositoryTests
{
    [Fact]
    public async Task AddAsync_ShouldPersistInvitation()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;
        var repo = new InvitationRepository(db);

        var invitation = GroupInvitation.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        await repo.AddAsync(invitation, CancellationToken.None);
        await db.SaveChangesAsync();

        var saved = await db.GroupInvitations.FirstOrDefaultAsync(x => x.Id == invitation.Id);
        saved.Should().NotBeNull();
        saved!.Status.Should().Be(InvitationStatus.Pending);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnInvitation_WhenExists()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var invitation = GroupInvitation.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        db.GroupInvitations.Add(invitation);
        await db.SaveChangesAsync();

        var repo = new InvitationRepository(db);

        var result = await repo.GetByIdAsync(invitation.Id, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(invitation.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenMissing()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;
        var repo = new InvitationRepository(db);

        var result = await repo.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task HasPendingAsync_ShouldReturnTrue_WhenPendingExists()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var groupId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();

        db.GroupInvitations.Add(GroupInvitation.Create(groupId, invitedUserId, Guid.NewGuid()));
        await db.SaveChangesAsync();

        var repo = new InvitationRepository(db);

        var result = await repo.HasPendingAsync(groupId, invitedUserId, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasPendingAsync_ShouldReturnFalse_WhenOnlyNonPendingExists()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var groupId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();

        var accepted = GroupInvitation.Create(groupId, invitedUserId, Guid.NewGuid());
        accepted.Accept();

        db.GroupInvitations.Add(accepted);
        await db.SaveChangesAsync();

        var repo = new InvitationRepository(db);

        var result = await repo.HasPendingAsync(groupId, invitedUserId, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetPendingByInvitedUserIdAsync_ShouldReturnOnlyPendingInvitations()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var invitedUserId = Guid.NewGuid();

        var pending1 = GroupInvitation.Create(Guid.NewGuid(), invitedUserId, Guid.NewGuid());
        var pending2 = GroupInvitation.Create(Guid.NewGuid(), invitedUserId, Guid.NewGuid());

        var rejected = GroupInvitation.Create(Guid.NewGuid(), invitedUserId, Guid.NewGuid());
        rejected.Reject();

        var otherUserPending = GroupInvitation.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        db.GroupInvitations.AddRange(pending1, pending2, rejected, otherUserPending);
        await db.SaveChangesAsync();

        var repo = new InvitationRepository(db);

        var result = await repo.GetPendingByInvitedUserIdAsync(invitedUserId, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(x =>
            x.InvitedUserId == invitedUserId &&
            x.Status == InvitationStatus.Pending);
    }

    [Fact]
    public async Task InviterIsActiveMemberAsync_ShouldReturnTrue_WhenActiveMemberExists()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var owner = User.Create(
            "owner-inviter-true@test.com",
            "hash",
            "owner_inviter_true",
            "Owner",
            "True");

        var inviter = User.Create(
            "inviter-true@test.com",
            "hash",
            "inviter_true",
            "Inviter",
            "True");

        var group = Group.Create("Invitation Group True", owner.Id);

        db.Users.AddRange(owner, inviter);
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        var member = GroupMember.CreateMember(group.Id, inviter.Id);

        db.GroupMembers.Add(member);
        await db.SaveChangesAsync();

        var repo = new InvitationRepository(db);

        var result = await repo.InviterIsActiveMemberAsync(group.Id, inviter.Id, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task InviterIsActiveMemberAsync_ShouldReturnFalse_WhenMembershipInactive()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var owner = User.Create(
            "owner-inviter-false@test.com",
            "hash",
            "owner_inviter_false",
            "Owner",
            "False");

        var inviter = User.Create(
            "inviter-false@test.com",
            "hash",
            "inviter_false",
            "Inviter",
            "False");

        var group = Group.Create("Invitation Group False", owner.Id);

        db.Users.AddRange(owner, inviter);
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        var member = GroupMember.CreateMember(group.Id, inviter.Id);
        member.Deactivate();

        db.GroupMembers.Add(member);
        await db.SaveChangesAsync();

        var repo = new InvitationRepository(db);

        var result = await repo.InviterIsActiveMemberAsync(group.Id, inviter.Id, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CancelPendingByInviterAsync_ShouldCancelOnlyPendingInvitationsOfSpecificInviter()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var groupId = Guid.NewGuid();
        var inviterId = Guid.NewGuid();
        var otherInviterId = Guid.NewGuid();

        var pending1 = GroupInvitation.Create(groupId, Guid.NewGuid(), inviterId);
        var pending2 = GroupInvitation.Create(groupId, Guid.NewGuid(), inviterId);

        var rejected = GroupInvitation.Create(groupId, Guid.NewGuid(), inviterId);
        rejected.Reject();

        var otherInviterPending = GroupInvitation.Create(groupId, Guid.NewGuid(), otherInviterId);

        db.GroupInvitations.AddRange(pending1, pending2, rejected, otherInviterPending);
        await db.SaveChangesAsync();

        var repo = new InvitationRepository(db);

        var count = await repo.CancelPendingByInviterAsync(groupId, inviterId, CancellationToken.None);

        count.Should().Be(2);
        pending1.Status.Should().Be(InvitationStatus.Cancelled);
        pending2.Status.Should().Be(InvitationStatus.Cancelled);
        rejected.Status.Should().Be(InvitationStatus.Rejected);
        otherInviterPending.Status.Should().Be(InvitationStatus.Pending);
    }

    [Fact]
    public async Task CancelPendingByGroupAsync_ShouldCancelOnlyPendingInvitationsOfSpecificGroup()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var groupId = Guid.NewGuid();
        var otherGroupId = Guid.NewGuid();

        var pending1 = GroupInvitation.Create(groupId, Guid.NewGuid(), Guid.NewGuid());
        var pending2 = GroupInvitation.Create(groupId, Guid.NewGuid(), Guid.NewGuid());

        var accepted = GroupInvitation.Create(groupId, Guid.NewGuid(), Guid.NewGuid());
        accepted.Accept();

        var otherGroupPending = GroupInvitation.Create(otherGroupId, Guid.NewGuid(), Guid.NewGuid());

        db.GroupInvitations.AddRange(pending1, pending2, accepted, otherGroupPending);
        await db.SaveChangesAsync();

        var repo = new InvitationRepository(db);

        var count = await repo.CancelPendingByGroupAsync(groupId, CancellationToken.None);

        count.Should().Be(2);
        pending1.Status.Should().Be(InvitationStatus.Cancelled);
        pending2.Status.Should().Be(InvitationStatus.Cancelled);
        accepted.Status.Should().Be(InvitationStatus.Accepted);
        otherGroupPending.Status.Should().Be(InvitationStatus.Pending);
    }
}