using InMoment.Domain.Media;
using InMoment.Infrastructure.Persistence;
using InMoment.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Tests.Persistence.Repositories;

public sealed class PhotoRepositoryTests
{
    [Fact]
    public async Task AddAsync_ShouldPersistPhoto()
    {
        await using var db = CreateDbContext();
        var repo = new PhotoRepository(db);

        var photo = CreatePhoto(Guid.NewGuid(), Guid.NewGuid(), "a.jpg");

        await repo.AddAsync(photo, CancellationToken.None);
        await db.SaveChangesAsync();

        var saved = await db.Photos.FirstOrDefaultAsync(x => x.Id == photo.Id);
        saved.Should().NotBeNull();
        saved!.StorageKey.Should().Be(photo.StorageKey);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnPhoto_WhenExists()
    {
        await using var db = CreateDbContext();
        var photo = CreatePhoto(Guid.NewGuid(), Guid.NewGuid(), "a.jpg");
        db.Photos.Add(photo);
        await db.SaveChangesAsync();

        var repo = new PhotoRepository(db);

        var result = await repo.GetByIdAsync(photo.Id, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(photo.Id);
    }

    [Fact]
    public async Task GetFeedByGroupAsync_ShouldReturnOnlyNonDeletedPhotos_OrderedDescending()
    {
        await using var db = CreateDbContext();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var oldest = CreatePhoto(groupId, userId, "oldest.jpg");
        var middle = CreatePhoto(groupId, userId, "middle.jpg");
        var newest = CreatePhoto(groupId, userId, "newest.jpg");
        var deleted = CreatePhoto(groupId, userId, "deleted.jpg");

        SetCreatedAt(oldest, new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(middle, new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(newest, new DateTime(2026, 1, 3, 10, 0, 0, DateTimeKind.Utc));
        MarkDeleted(deleted);

        db.Photos.AddRange(oldest, middle, newest, deleted);
        await db.SaveChangesAsync();

        var repo = new PhotoRepository(db);

        var result = await repo.GetFeedByGroupAsync(groupId, 10, CancellationToken.None);

        result.Select(x => x.Id).Should().ContainInOrder(newest.Id, middle.Id, oldest.Id);
        result.Should().NotContain(x => x.IsDeleted);
    }

    [Fact]
    public async Task GetFeedByGroupAsync_ShouldRespectLimit()
    {
        await using var db = CreateDbContext();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var p1 = CreatePhoto(groupId, userId, "1.jpg");
        var p2 = CreatePhoto(groupId, userId, "2.jpg");
        var p3 = CreatePhoto(groupId, userId, "3.jpg");

        SetCreatedAt(p1, new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(p2, new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(p3, new DateTime(2026, 1, 3, 10, 0, 0, DateTimeKind.Utc));

        db.Photos.AddRange(p1, p2, p3);
        await db.SaveChangesAsync();

        var repo = new PhotoRepository(db);

        var result = await repo.GetFeedByGroupAsync(groupId, 2, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(x => x.Id).Should().ContainInOrder(p3.Id, p2.Id);
    }

    [Fact]
    public async Task GetPageByGroupAsync_ShouldReturnLatestPage_WhenCursorNotProvided()
    {
        await using var db = CreateDbContext();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var p1 = CreatePhoto(groupId, userId, "1.jpg");
        var p2 = CreatePhoto(groupId, userId, "2.jpg");
        var p3 = CreatePhoto(groupId, userId, "3.jpg");

        SetCreatedAt(p1, new DateTime(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(p2, new DateTime(2026, 2, 2, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(p3, new DateTime(2026, 2, 3, 10, 0, 0, DateTimeKind.Utc));

        db.Photos.AddRange(p1, p2, p3);
        await db.SaveChangesAsync();

        var repo = new PhotoRepository(db);

        var result = await repo.GetPageByGroupAsync(groupId, 2, null, null, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(x => x.Id).Should().ContainInOrder(p3.Id, p2.Id);
    }

    [Fact]
    public async Task GetPageByGroupAsync_ShouldApplyCursor_ByCreatedAtAndId()
    {
        await using var db = CreateDbContext();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ts = new DateTime(2026, 2, 10, 10, 0, 0, DateTimeKind.Utc);

        var older = CreatePhoto(groupId, userId, "older.jpg");
        var sameTimeLowerId = CreatePhoto(groupId, userId, "same-low.jpg");
        var cursor = CreatePhoto(groupId, userId, "cursor.jpg");
        var newer = CreatePhoto(groupId, userId, "newer.jpg");

        SetCreatedAt(older, ts.AddDays(-1));
        SetCreatedAt(sameTimeLowerId, ts);
        SetCreatedAt(cursor, ts);
        SetCreatedAt(newer, ts.AddDays(1));

        // гарантируем порядок по Id внутри одинакового CreatedAt
        if (sameTimeLowerId.Id.CompareTo(cursor.Id) > 0)
        {
            (sameTimeLowerId, cursor) = (cursor, sameTimeLowerId);
            SetStorageKey(sameTimeLowerId, "same-low.jpg");
            SetStorageKey(cursor, "cursor.jpg");
        }

        db.Photos.AddRange(older, sameTimeLowerId, cursor, newer);
        await db.SaveChangesAsync();

        var repo = new PhotoRepository(db);

        var result = await repo.GetPageByGroupAsync(
            groupId,
            10,
            cursor.CreatedAt,
            cursor.Id,
            CancellationToken.None);

        result.Select(x => x.Id).Should().ContainInOrder(sameTimeLowerId.Id, older.Id);
        result.Should().NotContain(x => x.Id == cursor.Id);
        result.Should().NotContain(x => x.Id == newer.Id);
    }

    [Fact]
    public async Task GetByIdsAsync_ShouldReturnDictionary_ForRequestedIds()
    {
        await using var db = CreateDbContext();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var p1 = CreatePhoto(groupId, userId, "1.jpg");
        var p2 = CreatePhoto(groupId, userId, "2.jpg");
        var p3 = CreatePhoto(groupId, userId, "3.jpg");

        db.Photos.AddRange(p1, p2, p3);
        await db.SaveChangesAsync();

        var repo = new PhotoRepository(db);

        var result = await repo.GetByIdsAsync(new[] { p1.Id, p3.Id }, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().ContainKey(p1.Id);
        result.Should().ContainKey(p3.Id);
        result.Should().NotContainKey(p2.Id);
    }

    [Fact]
    public async Task GetByIdsAsync_ShouldReturnEmptyDictionary_WhenInputEmpty()
    {
        await using var db = CreateDbContext();
        var repo = new PhotoRepository(db);

        var result = await repo.GetByIdsAsync(Array.Empty<Guid>(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLatestByGroupAsync_ShouldReturnNewestNonDeletedPhoto()
    {
        await using var db = CreateDbContext();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var old = CreatePhoto(groupId, userId, "old.jpg");
        var latest = CreatePhoto(groupId, userId, "latest.jpg");
        var deletedNewest = CreatePhoto(groupId, userId, "deleted.jpg");

        SetCreatedAt(old, new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(latest, new DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(deletedNewest, new DateTime(2026, 3, 3, 10, 0, 0, DateTimeKind.Utc));
        MarkDeleted(deletedNewest);

        db.Photos.AddRange(old, latest, deletedNewest);
        await db.SaveChangesAsync();

        var repo = new PhotoRepository(db);

        var result = await repo.GetLatestByGroupAsync(groupId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(latest.Id);
    }

    [Fact]
    public async Task GetByGroupAndDateRangeAsync_ShouldReturnOnlyPhotosWithinRange_AndNonDeleted()
    {
        await using var db = CreateDbContext();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var before = CreatePhoto(groupId, userId, "before.jpg");
        var inside1 = CreatePhoto(groupId, userId, "inside1.jpg");
        var inside2 = CreatePhoto(groupId, userId, "inside2.jpg");
        var after = CreatePhoto(groupId, userId, "after.jpg");
        var deletedInside = CreatePhoto(groupId, userId, "deleted.jpg");

        var from = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 4, 12, 0, 0, 0, DateTimeKind.Utc);

        SetCreatedAt(before, new DateTime(2026, 4, 9, 23, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(inside1, new DateTime(2026, 4, 10, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(inside2, new DateTime(2026, 4, 11, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(after, new DateTime(2026, 4, 12, 0, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(deletedInside, new DateTime(2026, 4, 10, 12, 0, 0, DateTimeKind.Utc));
        MarkDeleted(deletedInside);

        db.Photos.AddRange(before, inside1, inside2, after, deletedInside);
        await db.SaveChangesAsync();

        var repo = new PhotoRepository(db);

        var result = await repo.GetByGroupAndDateRangeAsync(groupId, from, to, CancellationToken.None);

        result.Select(x => x.Id).Should().ContainInOrder(inside2.Id, inside1.Id);
        result.Should().NotContain(x => x.Id == before.Id || x.Id == after.Id || x.Id == deletedInside.Id);
    }

    [Fact]
    public async Task GetPostingDatesByGroupAsync_ShouldReturnDistinctSortedDates_ExcludingDeleted()
    {
        await using var db = CreateDbContext();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var d1a = CreatePhoto(groupId, userId, "d1a.jpg");
        var d1b = CreatePhoto(groupId, userId, "d1b.jpg");
        var d2 = CreatePhoto(groupId, userId, "d2.jpg");
        var deleted = CreatePhoto(groupId, userId, "deleted.jpg");

        SetCreatedAt(d1a, new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(d1b, new DateTime(2026, 5, 1, 20, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(d2, new DateTime(2026, 5, 2, 9, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(deleted, new DateTime(2026, 5, 3, 9, 0, 0, DateTimeKind.Utc));
        MarkDeleted(deleted);

        db.Photos.AddRange(d1a, d1b, d2, deleted);
        await db.SaveChangesAsync();

        var repo = new PhotoRepository(db);

        var result = await repo.GetPostingDatesByGroupAsync(groupId, CancellationToken.None);

        result.Should().Equal(
            new DateOnly(2026, 5, 1),
            new DateOnly(2026, 5, 2));
    }

    [Fact]
    public async Task CountByGroupAsync_ShouldCountOnlyNonDeletedPhotos()
    {
        await using var db = CreateDbContext();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var p1 = CreatePhoto(groupId, userId, "1.jpg");
        var p2 = CreatePhoto(groupId, userId, "2.jpg");
        var deleted = CreatePhoto(groupId, userId, "deleted.jpg");
        MarkDeleted(deleted);

        db.Photos.AddRange(p1, p2, deleted);
        await db.SaveChangesAsync();

        var repo = new PhotoRepository(db);

        var result = await repo.CountByGroupAsync(groupId, CancellationToken.None);

        result.Should().Be(2);
    }

    [Fact]
    public async Task GetByUserAndDateRangeAsync_ShouldReturnOnlyUserPhotosWithinRange_AndNonDeleted()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var before = CreatePhoto(groupId, userId, "before.jpg");
        var inside = CreatePhoto(groupId, userId, "inside.jpg");
        var otherUser = CreatePhoto(groupId, otherUserId, "other.jpg");
        var deleted = CreatePhoto(groupId, userId, "deleted.jpg");

        var from = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc);

        SetCreatedAt(before, new DateTime(2026, 6, 9, 23, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(inside, new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(otherUser, new DateTime(2026, 6, 10, 11, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(deleted, new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc));
        MarkDeleted(deleted);

        db.Photos.AddRange(before, inside, otherUser, deleted);
        await db.SaveChangesAsync();

        var repo = new PhotoRepository(db);

        var result = await repo.GetByUserAndDateRangeAsync(userId, from, to, CancellationToken.None);

        result.Should().ContainSingle(x => x.Id == inside.Id);
    }

    [Fact]
    public async Task GetPostingDatesByUserAsync_ShouldReturnDistinctSortedDates_ExcludingDeleted()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var d1 = CreatePhoto(groupId, userId, "d1.jpg");
        var d2 = CreatePhoto(groupId, userId, "d2.jpg");
        var deleted = CreatePhoto(groupId, userId, "deleted.jpg");

        SetCreatedAt(d1, new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(d2, new DateTime(2026, 7, 2, 8, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(deleted, new DateTime(2026, 7, 3, 8, 0, 0, DateTimeKind.Utc));
        MarkDeleted(deleted);

        db.Photos.AddRange(d1, d2, deleted);
        await db.SaveChangesAsync();

        var repo = new PhotoRepository(db);

        var result = await repo.GetPostingDatesByUserAsync(userId, CancellationToken.None);

        result.Should().Equal(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 2));
    }

    [Fact]
    public async Task CountByUserAsync_ShouldCountOnlyNonDeletedPhotos()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var p1 = CreatePhoto(groupId, userId, "1.jpg");
        var p2 = CreatePhoto(groupId, userId, "2.jpg");
        var deleted = CreatePhoto(groupId, userId, "deleted.jpg");
        MarkDeleted(deleted);

        db.Photos.AddRange(p1, p2, deleted);
        await db.SaveChangesAsync();

        var repo = new PhotoRepository(db);

        var result = await repo.CountByUserAsync(userId, CancellationToken.None);

        result.Should().Be(2);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PhotoRepositoryTests_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static Photo CreatePhoto(Guid groupId, Guid uploadedByUserId, string fileName)
        => Photo.Create(
            groupId: groupId,
            uploadedByUserId: uploadedByUserId,
            storageKey: $"groups/{groupId}/photos/{uploadedByUserId}/{fileName}",
            contentType: "image/jpeg",
            sizeBytes: 1024);

    private static void MarkDeleted(Photo photo)
    {
        var uploadedBy = photo.UploadedByUserId;
        photo.MarkDeleted(uploadedBy, uploadedBy);
    }

    private static void SetCreatedAt(Photo photo, DateTime createdAtUtc)
    {
        typeof(Photo)
            .GetProperty(nameof(Photo.CreatedAt))!
            .SetValue(photo, createdAtUtc);
    }

    private static void SetStorageKey(Photo photo, string storageKey)
    {
        typeof(Photo)
            .GetProperty(nameof(Photo.StorageKey))!
            .SetValue(photo, storageKey);
    }
}