using FluentAssertions;
using InMoment.Domain.Privacy;
using InMoment.Infrastructure.Persistence.Repositories;
using InMoment.Test.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Tests.Persistence.Repositories;

public sealed class BlockedUserRepositoryTests
{
    [Fact]
    public async Task AddAsync_ShouldPersistBlockedUser()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;
        var repo = new BlockedUserRepository(db);

        var entity = BlockedUser.Create(Guid.NewGuid(), Guid.NewGuid());

        await repo.AddAsync(entity, CancellationToken.None);
        await db.SaveChangesAsync();

        var saved = await db.BlockedUsers.FirstOrDefaultAsync(x => x.Id == entity.Id);

        saved.Should().NotBeNull();
        saved!.UserId.Should().Be(entity.UserId);
        saved.BlockedUserId.Should().Be(entity.BlockedUserId);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnBlockedUser_WhenExists()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var entity = BlockedUser.Create(Guid.NewGuid(), Guid.NewGuid());
        db.BlockedUsers.Add(entity);
        await db.SaveChangesAsync();

        var repo = new BlockedUserRepository(db);

        var result = await repo.GetAsync(entity.UserId, entity.BlockedUserId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(entity.Id);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNull_WhenMissing()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var repo = new BlockedUserRepository(testDb.DbContext);

        var result = await repo.GetAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenExactDirectionExists()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var entity = BlockedUser.Create(Guid.NewGuid(), Guid.NewGuid());
        db.BlockedUsers.Add(entity);
        await db.SaveChangesAsync();

        var repo = new BlockedUserRepository(db);

        var result = await repo.ExistsAsync(entity.UserId, entity.BlockedUserId, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsEitherDirectionAsync_ShouldReturnTrue_WhenReverseDirectionExists()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var entity = BlockedUser.Create(Guid.NewGuid(), Guid.NewGuid());
        db.BlockedUsers.Add(entity);
        await db.SaveChangesAsync();

        var repo = new BlockedUserRepository(db);

        var result = await repo.ExistsEitherDirectionAsync(
            entity.BlockedUserId,
            entity.UserId,
            CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetByUserIdAsync_ShouldReturnOrderedByCreatedAtDesc()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var userId = Guid.NewGuid();

        var oldest = BlockedUser.Create(userId, Guid.NewGuid());
        var middle = BlockedUser.Create(userId, Guid.NewGuid());
        var newest = BlockedUser.Create(userId, Guid.NewGuid());

        SetCreatedAtUtc(oldest, new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAtUtc(middle, new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAtUtc(newest, new DateTime(2026, 1, 3, 10, 0, 0, DateTimeKind.Utc));

        db.BlockedUsers.AddRange(oldest, middle, newest);
        await db.SaveChangesAsync();

        var repo = new BlockedUserRepository(db);

        var result = await repo.GetByUserIdAsync(userId, CancellationToken.None);

        result.Select(x => x.Id).Should().ContainInOrder(newest.Id, middle.Id, oldest.Id);
    }

    [Fact]
    public async Task Remove_ShouldDeleteBlockedUser()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var entity = BlockedUser.Create(Guid.NewGuid(), Guid.NewGuid());
        db.BlockedUsers.Add(entity);
        await db.SaveChangesAsync();

        var repo = new BlockedUserRepository(db);

        repo.Remove(entity);
        await db.SaveChangesAsync();

        var exists = await db.BlockedUsers.AnyAsync(x => x.Id == entity.Id);
        exists.Should().BeFalse();
    }

    private static void SetCreatedAtUtc(BlockedUser entity, DateTime dt)
    {
        typeof(BlockedUser)
            .GetProperty(nameof(BlockedUser.CreatedAtUtc))!
            .SetValue(entity, dt);
    }
}