using FluentAssertions;
using InMoment.Domain.Media;
using InMoment.Infrastructure.Persistence;
using InMoment.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Tests.Persistence.Repositories;

public sealed class SavedPhotoRepositoryTests
{
    [Fact]
    public async Task AddAsync_ShouldPersistSavedPhoto()
    {
        await using var db = CreateDbContext();
        var repo = new SavedPhotoRepository(db);

        var entity = SavedPhoto.Create(Guid.NewGuid(), Guid.NewGuid());

        await repo.AddAsync(entity, CancellationToken.None);
        await db.SaveChangesAsync();

        var saved = await db.SavedPhotos.FirstOrDefaultAsync(x => x.Id == entity.Id);

        saved.Should().NotBeNull();
        saved!.PhotoId.Should().Be(entity.PhotoId);
        saved.UserId.Should().Be(entity.UserId);
    }

    [Fact]
    public async Task GetByPhotoAndUserAsync_ShouldReturnSavedPhoto_WhenExists()
    {
        await using var db = CreateDbContext();

        var photoId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entity = SavedPhoto.Create(photoId, userId);

        db.SavedPhotos.Add(entity);
        await db.SaveChangesAsync();

        var repo = new SavedPhotoRepository(db);

        var result = await repo.GetByPhotoAndUserAsync(photoId, userId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(entity.Id);
    }

    [Fact]
    public async Task GetByPhotoAndUserAsync_ShouldReturnNull_WhenMissing()
    {
        await using var db = CreateDbContext();
        var repo = new SavedPhotoRepository(db);

        var result = await repo.GetByPhotoAndUserAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_ShouldDeleteSavedPhoto()
    {
        await using var db = CreateDbContext();

        var entity = SavedPhoto.Create(Guid.NewGuid(), Guid.NewGuid());
        db.SavedPhotos.Add(entity);
        await db.SaveChangesAsync();

        var repo = new SavedPhotoRepository(db);

        await repo.RemoveAsync(entity, CancellationToken.None);
        await db.SaveChangesAsync();

        var exists = await db.SavedPhotos.AnyAsync(x => x.Id == entity.Id);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task GetPageByUserAsync_ShouldReturnOrderedByCreatedAtDescThenIdDesc()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();

        var oldest = SavedPhoto.Create(Guid.NewGuid(), userId);
        var middle = SavedPhoto.Create(Guid.NewGuid(), userId);
        var newest = SavedPhoto.Create(Guid.NewGuid(), userId);

        SetCreatedAt(oldest, new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(middle, new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(newest, new DateTime(2026, 1, 3, 10, 0, 0, DateTimeKind.Utc));

        db.SavedPhotos.AddRange(oldest, middle, newest);
        await db.SaveChangesAsync();

        var repo = new SavedPhotoRepository(db);

        var result = await repo.GetPageByUserAsync(userId, 10, null, null, CancellationToken.None);

        result.Select(x => x.Id).Should().ContainInOrder(newest.Id, middle.Id, oldest.Id);
    }

    [Fact]
    public async Task GetPageByUserAsync_ShouldRespectLimit()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();

        var a = SavedPhoto.Create(Guid.NewGuid(), userId);
        var b = SavedPhoto.Create(Guid.NewGuid(), userId);
        var c = SavedPhoto.Create(Guid.NewGuid(), userId);

        SetCreatedAt(a, new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(b, new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(c, new DateTime(2026, 1, 3, 10, 0, 0, DateTimeKind.Utc));

        db.SavedPhotos.AddRange(a, b, c);
        await db.SaveChangesAsync();

        var repo = new SavedPhotoRepository(db);

        var result = await repo.GetPageByUserAsync(userId, 2, null, null, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(x => x.Id).Should().ContainInOrder(c.Id, b.Id);
    }

    [Fact]
    public async Task GetPageByUserAsync_ShouldRespectCursor()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();

        var older = SavedPhoto.Create(Guid.NewGuid(), userId);
        var sameTimeLowerId = SavedPhoto.Create(Guid.NewGuid(), userId);
        var cursor = SavedPhoto.Create(Guid.NewGuid(), userId);
        var newer = SavedPhoto.Create(Guid.NewGuid(), userId);

        var ts = new DateTime(2026, 2, 10, 10, 0, 0, DateTimeKind.Utc);

        SetCreatedAt(older, ts.AddDays(-1));
        SetCreatedAt(sameTimeLowerId, ts);
        SetCreatedAt(cursor, ts);
        SetCreatedAt(newer, ts.AddDays(1));

        if (sameTimeLowerId.Id.CompareTo(cursor.Id) > 0)
        {
            (sameTimeLowerId, cursor) = (cursor, sameTimeLowerId);
        }

        db.SavedPhotos.AddRange(older, sameTimeLowerId, cursor, newer);
        await db.SaveChangesAsync();

        var repo = new SavedPhotoRepository(db);

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

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"SavedPhotoRepositoryTests_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static void SetCreatedAt(SavedPhoto entity, DateTime dt)
    {
        typeof(SavedPhoto)
            .GetProperty(nameof(SavedPhoto.CreatedAt))!
            .SetValue(entity, dt);
    }
}