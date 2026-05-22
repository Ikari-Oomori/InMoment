using FluentAssertions;
using InMoment.Domain.Friends;
using InMoment.Infrastructure.Persistence.Repositories;
using InMoment.Test.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Tests.Persistence.Repositories;

public sealed class FriendshipRepositoryTests
{
    [Fact]
    public async Task AddAsync_ShouldPersistFriendship()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;
        var repo = new FriendshipRepository(db);

        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var entity = Friendship.Create(userA, userB);

        await repo.AddAsync(entity, CancellationToken.None);
        await db.SaveChangesAsync();

        var saved = await db.Friendships.FirstOrDefaultAsync(x => x.Id == entity.Id);

        saved.Should().NotBeNull();
        saved!.User1Id.Should().Be(entity.User1Id);
        saved.User2Id.Should().Be(entity.User2Id);
    }

    [Fact]
    public async Task GetByUsersAsync_ShouldReturnFriendship_RegardlessOfInputOrder()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var entity = Friendship.Create(userA, userB);

        db.Friendships.Add(entity);
        await db.SaveChangesAsync();

        var repo = new FriendshipRepository(db);

        var result = await repo.GetByUsersAsync(userB, userA, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(entity.Id);
    }

    [Fact]
    public async Task GetByUsersAsync_ShouldReturnNull_WhenMissing()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var repo = new FriendshipRepository(testDb.DbContext);

        var result = await repo.GetByUsersAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserIdAsync_ShouldReturnAllFriendshipsForUser_OrderedByCreatedAtDesc()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var currentUserId = Guid.NewGuid();

        var oldest = Friendship.Create(currentUserId, Guid.NewGuid());
        var newest = Friendship.Create(Guid.NewGuid(), currentUserId);
        var unrelated = Friendship.Create(Guid.NewGuid(), Guid.NewGuid());

        SetCreatedAtUtc(oldest, new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAtUtc(newest, new DateTime(2026, 4, 2, 10, 0, 0, DateTimeKind.Utc));

        db.Friendships.AddRange(oldest, newest, unrelated);
        await db.SaveChangesAsync();

        var repo = new FriendshipRepository(db);

        var result = await repo.GetByUserIdAsync(currentUserId, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(x => x.Id).Should().ContainInOrder(newest.Id, oldest.Id);
    }

    [Fact]
    public async Task Remove_ShouldDeleteFriendship()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var entity = Friendship.Create(Guid.NewGuid(), Guid.NewGuid());
        db.Friendships.Add(entity);
        await db.SaveChangesAsync();

        var repo = new FriendshipRepository(db);

        repo.Remove(entity);
        await db.SaveChangesAsync();

        var exists = await db.Friendships.AnyAsync(x => x.Id == entity.Id);
        exists.Should().BeFalse();
    }

    private static void SetCreatedAtUtc(Friendship entity, DateTime dt)
    {
        typeof(Friendship)
            .GetProperty(nameof(Friendship.CreatedAtUtc))!
            .SetValue(entity, dt);
    }
}