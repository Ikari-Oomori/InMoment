using FluentAssertions;
using InMoment.Domain.Notifications;
using InMoment.Infrastructure.Persistence;
using InMoment.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Tests.Persistence.Repositories;

public sealed class NotificationRepositoryTests
{
    [Fact]
    public async Task AddAsync_ShouldPersistNotification()
    {
        await using var db = CreateDbContext();
        var repo = new NotificationRepository(db);

        var entity = Notification.CreateReactionOnPhoto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());

        await repo.AddAsync(entity, CancellationToken.None);
        await db.SaveChangesAsync();

        var saved = await db.Notifications.FirstOrDefaultAsync(x => x.Id == entity.Id);

        saved.Should().NotBeNull();
        saved!.UserId.Should().Be(entity.UserId);
        saved.Type.Should().Be(NotificationType.ReactionOnPhoto);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNotification_WhenExists()
    {
        await using var db = CreateDbContext();

        var entity = Notification.CreateCommentOnPhoto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());

        db.Notifications.Add(entity);
        await db.SaveChangesAsync();

        var repo = new NotificationRepository(db);

        var result = await repo.GetByIdAsync(entity.Id, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(entity.Id);
    }

    [Fact]
    public async Task GetPageByUserAsync_ShouldReturnOrderedByCreatedAtDescThenIdDesc()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();

        var oldest = Notification.CreateReactionOnPhoto(userId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var middle = Notification.CreateReactionOnPhoto(userId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var newest = Notification.CreateReactionOnPhoto(userId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        SetCreatedAt(oldest, new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(middle, new DateTime(2026, 5, 2, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(newest, new DateTime(2026, 5, 3, 10, 0, 0, DateTimeKind.Utc));

        db.Notifications.AddRange(oldest, middle, newest);
        await db.SaveChangesAsync();

        var repo = new NotificationRepository(db);

        var result = await repo.GetPageByUserAsync(userId, 10, null, null, CancellationToken.None);

        result.Select(x => x.Id).Should().ContainInOrder(newest.Id, middle.Id, oldest.Id);
    }

    [Fact]
    public async Task GetPageByUserAsync_ShouldRespectLimit()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();

        var a = Notification.CreateReactionOnPhoto(userId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var b = Notification.CreateReactionOnPhoto(userId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var c = Notification.CreateReactionOnPhoto(userId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        SetCreatedAt(a, new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(b, new DateTime(2026, 5, 2, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(c, new DateTime(2026, 5, 3, 10, 0, 0, DateTimeKind.Utc));

        db.Notifications.AddRange(a, b, c);
        await db.SaveChangesAsync();

        var repo = new NotificationRepository(db);

        var result = await repo.GetPageByUserAsync(userId, 2, null, null, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(x => x.Id).Should().ContainInOrder(c.Id, b.Id);
    }

    [Fact]
    public async Task GetPageByUserAsync_ShouldRespectCursor()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();

        var older = Notification.CreateReactionOnPhoto(userId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var sameTimeLowerId = Notification.CreateReactionOnPhoto(userId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var cursor = Notification.CreateReactionOnPhoto(userId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var newer = Notification.CreateReactionOnPhoto(userId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var ts = new DateTime(2026, 5, 10, 10, 0, 0, DateTimeKind.Utc);

        SetCreatedAt(older, ts.AddDays(-1));
        SetCreatedAt(sameTimeLowerId, ts);
        SetCreatedAt(cursor, ts);
        SetCreatedAt(newer, ts.AddDays(1));

        if (sameTimeLowerId.Id.CompareTo(cursor.Id) > 0)
        {
            (sameTimeLowerId, cursor) = (cursor, sameTimeLowerId);
        }

        db.Notifications.AddRange(older, sameTimeLowerId, cursor, newer);
        await db.SaveChangesAsync();

        var repo = new NotificationRepository(db);

        var result = await repo.GetPageByUserAsync(
            userId,
            10,
            cursor.CreatedAt,
            cursor.Id,
            CancellationToken.None);

        result.Select(x => x.Id).Should().ContainInOrder(sameTimeLowerId.Id, older.Id);
        result.Should().NotContain(x => x.Id == cursor.Id);
        result.Should().NotContain(x => x.Id == newer.Id);
    }

    [Fact]
    public async Task GetUnreadByUserAsync_ShouldReturnOnlyUnread_OrderedByCreatedAtAsc()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();

        var oldestUnread = Notification.CreateReactionOnPhoto(userId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var newestUnread = Notification.CreateCommentOnPhoto(userId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var read = Notification.CreateReplyToComment(userId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        read.MarkRead();

        SetCreatedAt(oldestUnread, new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(newestUnread, new DateTime(2026, 6, 2, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(read, new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Utc));

        db.Notifications.AddRange(oldestUnread, newestUnread, read);
        await db.SaveChangesAsync();

        var repo = new NotificationRepository(db);

        var result = await repo.GetUnreadByUserAsync(userId, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(x => x.Id).Should().ContainInOrder(oldestUnread.Id, newestUnread.Id);
    }

    [Fact]
    public async Task GetUnreadCountAsync_ShouldReturnOnlyUnreadCount()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();

        var unread1 = Notification.CreateReactionOnPhoto(userId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var unread2 = Notification.CreateCommentOnPhoto(userId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var read = Notification.CreateReplyToComment(userId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        read.MarkRead();

        db.Notifications.AddRange(unread1, unread2, read);
        await db.SaveChangesAsync();

        var repo = new NotificationRepository(db);

        var result = await repo.GetUnreadCountAsync(userId, CancellationToken.None);

        result.Should().Be(2);
    }

    [Fact]
    public async Task FindLatestUnreadCollapsibleAsync_ShouldReturnLatestMatchingUnread()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        var older = Notification.CreateReactionOnPhoto(userId, actorId, groupId, photoId);
        var latest = Notification.CreateReactionOnPhoto(userId, actorId, groupId, photoId);
        var otherType = Notification.CreateCommentOnPhoto(userId, actorId, groupId, photoId, Guid.NewGuid());
        var read = Notification.CreateReactionOnPhoto(userId, actorId, groupId, photoId);
        read.MarkRead();

        SetCreatedAt(older, new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(latest, new DateTime(2026, 7, 2, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(otherType, new DateTime(2026, 7, 3, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(read, new DateTime(2026, 7, 4, 10, 0, 0, DateTimeKind.Utc));

        db.Notifications.AddRange(older, latest, otherType, read);
        await db.SaveChangesAsync();

        var repo = new NotificationRepository(db);

        var result = await repo.FindLatestUnreadCollapsibleAsync(
            userId,
            NotificationType.ReactionOnPhoto,
            actorId,
            groupId,
            photoId,
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(latest.Id);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"NotificationRepositoryTests_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static void SetCreatedAt(Notification entity, DateTime dt)
    {
        typeof(Notification)
            .GetProperty(nameof(Notification.CreatedAt))!
            .SetValue(entity, dt);
    }
}