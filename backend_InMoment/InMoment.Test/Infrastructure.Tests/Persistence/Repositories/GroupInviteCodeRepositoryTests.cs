using FluentAssertions;
using InMoment.Domain.Groups;
using InMoment.Infrastructure.Persistence;
using InMoment.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Tests.Persistence.Repositories;

public sealed class GroupInviteCodeRepositoryTests
{
    [Fact]
    public async Task AddAsync_ShouldPersistInviteCode()
    {
        await using var db = CreateDbContext();
        var repo = new GroupInviteCodeRepository(db);

        var code = GroupInviteCode.Create(
            groupId: Guid.NewGuid(),
            code: "ABC123",
            createdByUserId: Guid.NewGuid(),
            createdAtUtc: DateTime.UtcNow,
            expiresAtUtc: DateTime.UtcNow.AddDays(1),
            maxUses: 10);

        await repo.AddAsync(code, CancellationToken.None);
        await db.SaveChangesAsync();

        var saved = await db.GroupInviteCodes.FirstOrDefaultAsync(x => x.Id == code.Id);
        saved.Should().NotBeNull();
        saved!.Code.Should().Be("ABC123");
    }

    [Fact]
    public async Task GetByCodeAsync_ShouldReturnCode_WhenExists()
    {
        await using var db = CreateDbContext();

        var code = GroupInviteCode.Create(
            groupId: Guid.NewGuid(),
            code: "ABC123",
            createdByUserId: Guid.NewGuid(),
            createdAtUtc: DateTime.UtcNow,
            expiresAtUtc: DateTime.UtcNow.AddDays(1),
            maxUses: 10);

        db.GroupInviteCodes.Add(code);
        await db.SaveChangesAsync();

        var repo = new GroupInviteCodeRepository(db);

        var result = await repo.GetByCodeAsync("ABC123", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(code.Id);
    }

    [Fact]
    public async Task GetByCodeAsync_ShouldReturnNull_WhenMissing()
    {
        await using var db = CreateDbContext();
        var repo = new GroupInviteCodeRepository(db);

        var result = await repo.GetByCodeAsync("MISSING", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByGroupIdAsync_ShouldReturnCodesOrderedByCreatedAtDescendingThenIdDescending()
    {
        await using var db = CreateDbContext();

        var groupId = Guid.NewGuid();

        var oldest = GroupInviteCode.Create(
            groupId,
            "CODE1",
            Guid.NewGuid(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1),
            10);

        var middle = GroupInviteCode.Create(
            groupId,
            "CODE2",
            Guid.NewGuid(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1),
            10);

        var newest = GroupInviteCode.Create(
            groupId,
            "CODE3",
            Guid.NewGuid(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1),
            10);

        var otherGroup = GroupInviteCode.Create(
            Guid.NewGuid(),
            "CODE4",
            Guid.NewGuid(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1),
            10);

        SetCreatedAt(oldest, new DateTime(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(middle, new DateTime(2026, 2, 2, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(newest, new DateTime(2026, 2, 3, 10, 0, 0, DateTimeKind.Utc));

        db.GroupInviteCodes.AddRange(oldest, middle, newest, otherGroup);
        await db.SaveChangesAsync();

        var repo = new GroupInviteCodeRepository(db);

        var result = await repo.GetByGroupIdAsync(groupId, CancellationToken.None);

        result.Should().HaveCount(3);
        result.Select(x => x.Id).Should().ContainInOrder(newest.Id, middle.Id, oldest.Id);
        result.Should().OnlyContain(x => x.GroupId == groupId);
    }

    [Fact]
    public async Task GetByGroupIdAsync_ShouldReturnEmpty_WhenGroupHasNoCodes()
    {
        await using var db = CreateDbContext();
        var repo = new GroupInviteCodeRepository(db);

        var result = await repo.GetByGroupIdAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"GroupInviteCodeRepositoryTests_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static void SetCreatedAt(GroupInviteCode code, DateTime createdAtUtc)
    {
        typeof(GroupInviteCode)
            .GetProperty(nameof(GroupInviteCode.CreatedAtUtc))!
            .SetValue(code, createdAtUtc);
    }
}