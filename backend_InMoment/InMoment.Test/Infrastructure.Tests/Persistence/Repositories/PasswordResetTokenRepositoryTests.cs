using FluentAssertions;
using InMoment.Domain.Security;
using InMoment.Infrastructure.Persistence;
using InMoment.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Tests.Persistence.Repositories;

public sealed class PasswordResetTokenRepositoryTests
{
    [Fact]
    public async Task AddAsync_ShouldPersistToken()
    {
        await using var db = CreateDbContext();
        var repo = new PasswordResetTokenRepository(db);

        var token = PasswordResetToken.Create(
            Guid.NewGuid(),
            "token-hash",
            DateTime.UtcNow.AddMinutes(-5),
            DateTime.UtcNow.AddHours(1),
            "127.0.0.1",
            "agent");

        await repo.AddAsync(token, CancellationToken.None);
        await db.SaveChangesAsync();

        var saved = await db.PasswordResetTokens.FirstOrDefaultAsync(x => x.Id == token.Id);

        saved.Should().NotBeNull();
        saved!.TokenHash.Should().Be("token-hash");
        saved.UserId.Should().Be(token.UserId);
    }

    [Fact]
    public async Task GetByTokenHashAsync_ShouldReturnToken_WhenExists()
    {
        await using var db = CreateDbContext();

        var token = PasswordResetToken.Create(
            Guid.NewGuid(),
            "token-hash",
            DateTime.UtcNow.AddMinutes(-5),
            DateTime.UtcNow.AddHours(1),
            null,
            null);

        db.PasswordResetTokens.Add(token);
        await db.SaveChangesAsync();

        var repo = new PasswordResetTokenRepository(db);

        var result = await repo.GetByTokenHashAsync("token-hash", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(token.Id);
    }

    [Fact]
    public async Task GetByTokenHashAsync_ShouldReturnNull_WhenMissing()
    {
        await using var db = CreateDbContext();
        var repo = new PasswordResetTokenRepository(db);

        var result = await repo.GetByTokenHashAsync("missing", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveByUserIdAsync_ShouldReturnOnlyActiveTokensOrderedByCreatedAtDescending()
    {
        await using var db = CreateDbContext();

        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var oldActive = PasswordResetToken.Create(
            userId,
            "hash-1",
            now.AddHours(-3),
            now.AddHours(2),
            null,
            null);

        var newActive = PasswordResetToken.Create(
            userId,
            "hash-2",
            now.AddHours(-1),
            now.AddHours(3),
            null,
            null);

        var used = PasswordResetToken.Create(
            userId,
            "hash-3",
            now.AddHours(-2),
            now.AddHours(2),
            null,
            null);
        used.MarkUsed(now.AddMinutes(-30));

        var revoked = PasswordResetToken.Create(
            userId,
            "hash-4",
            now.AddHours(-2),
            now.AddHours(2),
            null,
            null);
        revoked.Revoke(now.AddMinutes(-20));

        var expired = PasswordResetToken.Create(
            userId,
            "hash-5",
            now.AddHours(-3),
            now.AddMinutes(-1),
            null,
            null);

        var otherUser = PasswordResetToken.Create(
            Guid.NewGuid(),
            "hash-6",
            now.AddHours(-1),
            now.AddHours(2),
            null,
            null);

        db.PasswordResetTokens.AddRange(oldActive, newActive, used, revoked, expired, otherUser);
        await db.SaveChangesAsync();

        var repo = new PasswordResetTokenRepository(db);

        var result = await repo.GetActiveByUserIdAsync(userId, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(x => x.Id).Should().ContainInOrder(newActive.Id, oldActive.Id);
        result.Should().OnlyContain(x =>
            x.UserId == userId &&
            x.UsedAtUtc == null &&
            x.RevokedAtUtc == null);
    }

    [Fact]
    public async Task GetActiveByUserIdAsync_ShouldReturnEmpty_WhenUserHasNoActiveTokens()
    {
        await using var db = CreateDbContext();

        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var expired = PasswordResetToken.Create(
            userId,
            "hash-1",
            now.AddHours(-2),
            now.AddMinutes(-1),
            null,
            null);

        var used = PasswordResetToken.Create(
            userId,
            "hash-2",
            now.AddHours(-2),
            now.AddHours(1),
            null,
            null);
        used.MarkUsed(now);

        db.PasswordResetTokens.AddRange(expired, used);
        await db.SaveChangesAsync();

        var repo = new PasswordResetTokenRepository(db);

        var result = await repo.GetActiveByUserIdAsync(userId, CancellationToken.None);

        result.Should().BeEmpty();
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PasswordResetTokenRepositoryTests_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }
}