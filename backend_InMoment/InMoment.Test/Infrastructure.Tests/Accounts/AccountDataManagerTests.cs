using FluentAssertions;
using InMoment.Domain.Common;
using InMoment.Domain.Friends;
using InMoment.Domain.Groups;
using InMoment.Domain.Media;
using InMoment.Domain.Notifications;
using InMoment.Domain.Security;
using InMoment.Domain.Users;
using InMoment.Infrastructure.Accounts;
using InMoment.Test.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Tests.Accounts;

public sealed class AccountDataManagerTests
{
    [Fact]
    public async Task GetSummaryAsync_ShouldThrowNotFoundException_WhenUserMissing()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var manager = new AccountDataManager(testDb.DbContext);

        var act = () => manager.GetSummaryAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Пользователь не найден.");
    }

    [Fact]
    public async Task GetSummaryAsync_ShouldReturnCalculatedCounters()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var user = User.Create("user@test.com", "hash", "user1", "Test", "User");
        var friend = User.Create("friend@test.com", "hash", "friend1", "Friend", "User");
        var owner = User.Create("owner@test.com", "hash", "owner1", "Owner", "User");

        var ownedGroup = Group.Create("Owned", user.Id);
        var joinedGroup = Group.Create("Joined", owner.Id);
        joinedGroup.AddMember(user.Id);

        var photo1 = Photo.Create(ownedGroup.Id, user.Id, "photos/1.jpg", "image/jpeg", 100);
        var photo2 = Photo.Create(joinedGroup.Id, user.Id, "photos/2.jpg", "image/jpeg", 100);
        var deletedPhoto = Photo.Create(joinedGroup.Id, user.Id, "photos/3.jpg", "image/jpeg", 100);
        deletedPhoto.MarkDeleted(user.Id, owner.Id);

        var comment1 = Comment.CreateRoot(photo1.Id, user.Id, "comment 1");
        var comment2 = Comment.CreateRoot(photo2.Id, user.Id, "comment 2");
        var deletedComment = Comment.CreateRoot(photo2.Id, user.Id, "deleted");
        deletedComment.Delete(user.Id);

        var reaction1 = Reaction.Create(photo1.Id, user.Id, ReactionType.Heart);
        var reaction2 = Reaction.Create(photo2.Id, user.Id, ReactionType.Wow);

        var friendship = Friendship.Create(user.Id, friend.Id);

        var activeSession = RefreshSession.Create(
            user.Id,
            "token-hash-1",
            DateTime.UtcNow.AddMinutes(-5),
            DateTime.UtcNow.AddDays(10),
            "iPhone",
            "iOS",
            "127.0.0.1",
            "UA",
            null,
            null,
            null,
            null);

        var expiredSession = RefreshSession.Create(
            user.Id,
            "token-hash-2",
            DateTime.UtcNow.AddDays(-40),
            DateTime.UtcNow.AddDays(-1),
            "Pixel",
            "Android",
            "127.0.0.2",
            "UA",
            null,
            null,
            null,
            null);

        db.Users.AddRange(user, friend, owner);
        db.Groups.AddRange(ownedGroup, joinedGroup);
        db.Photos.AddRange(photo1, photo2, deletedPhoto);
        db.Comments.AddRange(comment1, comment2, deletedComment);
        db.Reactions.AddRange(reaction1, reaction2);
        db.Set<Friendship>().Add(friendship);
        db.Set<RefreshSession>().AddRange(activeSession, expiredSession);

        await db.SaveChangesAsync();

        var manager = new AccountDataManager(db);

        var result = await manager.GetSummaryAsync(user.Id, CancellationToken.None);

        result.UserId.Should().Be(user.Id);
        result.IsActive.Should().BeTrue();
        result.GroupsCount.Should().Be(2);
        result.OwnedGroupsCount.Should().Be(1);
        result.PhotosCount.Should().Be(2);
        result.CommentsCount.Should().Be(2);
        result.ReactionsCount.Should().Be(2);
        result.FriendshipsCount.Should().Be(1);
        result.ActiveSessionsCount.Should().Be(1);
    }

    [Fact]
    public async Task DeactivateAccountAsync_ShouldReturn_WhenAlreadyInactive()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var user = User.Create("user@test.com", "hash", "user1", "Test", "User");
        user.Deactivate();

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var manager = new AccountDataManager(db);

        await manager.DeactivateAccountAsync(user.Id, CancellationToken.None);

        var stored = await db.Users.FirstAsync(x => x.Id == user.Id);
        stored.IsActive.Should().BeFalse();
        stored.ActiveGroupId.Should().BeNull();
    }

    [Fact]
    public async Task DeactivateAccountAsync_ShouldDeactivateUser_LeaveGroups_RevokeSessions_AndMarkNotificationsRead()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var user = User.Create("user@test.com", "hash", "user1", "Test", "User");
        var owner = User.Create("owner@test.com", "hash", "owner1", "Owner", "User");
        var member = User.Create("member@test.com", "hash", "member1", "Member", "User");
        var soloOwnedGroup = Group.Create("Solo Owned Group", user.Id);
        var joinedGroup = Group.Create("Joined Group", owner.Id);
        joinedGroup.AddMember(user.Id);
        joinedGroup.AddMember(member.Id);

        user.SetActiveGroup(joinedGroup.Id);

        var activeSession = RefreshSession.Create(
            user.Id,
            "token-hash-1",
            DateTime.UtcNow.AddMinutes(-5),
            DateTime.UtcNow.AddDays(10),
            "iPhone",
            "iOS",
            "127.0.0.1",
            "UA",
            null,
            null,
            null,
            null);

        var revokedSession = RefreshSession.Create(
            user.Id,
            "token-hash-2",
            DateTime.UtcNow.AddMinutes(-10),
            DateTime.UtcNow.AddDays(10),
            "Pixel",
            "Android",
            "127.0.0.2",
            "UA",
            null,
            null,
            null,
            null);
        revokedSession.Revoke("manual", DateTime.UtcNow);

        var unread = Notification.CreateReactionOnPhoto(
            user.Id,
            owner.Id,
            joinedGroup.Id,
            Guid.NewGuid());

        var alreadyRead = Notification.CreateCommentOnPhoto(
            user.Id,
            member.Id,
            joinedGroup.Id,
            Guid.NewGuid(),
            Guid.NewGuid());
        alreadyRead.MarkRead();

        db.Users.AddRange(user, owner, member);
        db.Groups.AddRange(soloOwnedGroup, joinedGroup);
        db.Set<RefreshSession>().AddRange(activeSession, revokedSession);
        db.Notifications.AddRange(unread, alreadyRead);

        await db.SaveChangesAsync();

        var manager = new AccountDataManager(db);

        await manager.DeactivateAccountAsync(user.Id, CancellationToken.None);

        var storedUser = await db.Users.FirstAsync(x => x.Id == user.Id);
        var storedSoloOwnedGroup = await db.Groups.Include(x => x.Members).FirstAsync(x => x.Id == soloOwnedGroup.Id);
        var storedJoinedGroup = await db.Groups.Include(x => x.Members).FirstAsync(x => x.Id == joinedGroup.Id);
        var storedSessions = await db.Set<RefreshSession>().Where(x => x.UserId == user.Id).ToListAsync();
        var storedNotifications = await db.Notifications.Where(x => x.UserId == user.Id).ToListAsync();

        storedUser.IsActive.Should().BeFalse();
        storedUser.ActiveGroupId.Should().BeNull();

        storedSoloOwnedGroup.IsActive.Should().BeFalse();
        storedSoloOwnedGroup.IsMember(user.Id).Should().BeFalse();

        storedJoinedGroup.IsActive.Should().BeTrue();
        storedJoinedGroup.OwnerId.Should().Be(owner.Id);
        storedJoinedGroup.IsMember(user.Id).Should().BeFalse();
        storedJoinedGroup.IsMember(member.Id).Should().BeTrue();

        storedSessions.Should().OnlyContain(x => x.RevokedAtUtc != null);
        storedNotifications.Should().OnlyContain(x => x.IsRead);
    }
}