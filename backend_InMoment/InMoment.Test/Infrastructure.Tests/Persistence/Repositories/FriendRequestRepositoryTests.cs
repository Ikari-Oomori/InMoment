using FluentAssertions;
using InMoment.Domain.Friends;
using InMoment.Infrastructure.Persistence.Repositories;
using InMoment.Test.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Tests.Persistence.Repositories;

public sealed class FriendRequestRepositoryTests
{
    [Fact]
    public async Task AddAsync_ShouldPersistRequest()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;
        var repo = new FriendRequestRepository(db);

        var entity = FriendRequest.Create(Guid.NewGuid(), Guid.NewGuid());

        await repo.AddAsync(entity, CancellationToken.None);
        await db.SaveChangesAsync();

        var saved = await db.FriendRequests.FirstOrDefaultAsync(x => x.Id == entity.Id);

        saved.Should().NotBeNull();
        saved!.FromUserId.Should().Be(entity.FromUserId);
        saved.ToUserId.Should().Be(entity.ToUserId);
        saved.Status.Should().Be(FriendRequestStatus.Pending);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnRequest_WhenExists()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var entity = FriendRequest.Create(Guid.NewGuid(), Guid.NewGuid());
        db.FriendRequests.Add(entity);
        await db.SaveChangesAsync();

        var repo = new FriendRequestRepository(db);

        var result = await repo.GetByIdAsync(entity.Id, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(entity.Id);
    }

    [Fact]
    public async Task GetPendingBetweenUsersAsync_ShouldReturnPendingRequest_InAnyDirection()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var entity = FriendRequest.Create(userA, userB);
        db.FriendRequests.Add(entity);
        await db.SaveChangesAsync();

        var repo = new FriendRequestRepository(db);

        var result = await repo.GetPendingBetweenUsersAsync(userB, userA, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(entity.Id);
    }

    [Fact]
    public async Task GetPendingBetweenUsersAsync_ShouldIgnoreNonPendingRequests()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var entity = FriendRequest.Create(userA, userB);
        entity.Accept();

        db.FriendRequests.Add(entity);
        await db.SaveChangesAsync();

        var repo = new FriendRequestRepository(db);

        var result = await repo.GetPendingBetweenUsersAsync(userA, userB, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetIncomingPendingAsync_ShouldReturnOnlyIncomingPending_OrderedByCreatedAtDesc()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var targetUserId = Guid.NewGuid();

        var oldest = FriendRequest.Create(Guid.NewGuid(), targetUserId);
        var newest = FriendRequest.Create(Guid.NewGuid(), targetUserId);
        var outgoing = FriendRequest.Create(targetUserId, Guid.NewGuid());
        var accepted = FriendRequest.Create(Guid.NewGuid(), targetUserId);
        accepted.Accept();

        SetCreatedAtUtc(oldest, new DateTime(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAtUtc(newest, new DateTime(2026, 2, 2, 10, 0, 0, DateTimeKind.Utc));

        db.FriendRequests.AddRange(oldest, newest, outgoing, accepted);
        await db.SaveChangesAsync();

        var repo = new FriendRequestRepository(db);

        var result = await repo.GetIncomingPendingAsync(targetUserId, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(x => x.Id).Should().ContainInOrder(newest.Id, oldest.Id);
    }

    [Fact]
    public async Task GetOutgoingPendingAsync_ShouldReturnOnlyOutgoingPending_OrderedByCreatedAtDesc()
    {
        await using var testDb = SqliteDbContextFactory.Create();
        var db = testDb.DbContext;

        var currentUserId = Guid.NewGuid();

        var oldest = FriendRequest.Create(currentUserId, Guid.NewGuid());
        var newest = FriendRequest.Create(currentUserId, Guid.NewGuid());
        var incoming = FriendRequest.Create(Guid.NewGuid(), currentUserId);
        var cancelled = FriendRequest.Create(currentUserId, Guid.NewGuid());
        cancelled.Cancel();

        SetCreatedAtUtc(oldest, new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAtUtc(newest, new DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc));

        db.FriendRequests.AddRange(oldest, newest, incoming, cancelled);
        await db.SaveChangesAsync();

        var repo = new FriendRequestRepository(db);

        var result = await repo.GetOutgoingPendingAsync(currentUserId, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(x => x.Id).Should().ContainInOrder(newest.Id, oldest.Id);
    }

    private static void SetCreatedAtUtc(FriendRequest entity, DateTime dt)
    {
        typeof(FriendRequest)
            .GetProperty(nameof(FriendRequest.CreatedAtUtc))!
            .SetValue(entity, dt);
    }
}