using InMoment.Domain.Media;
using InMoment.Infrastructure.Persistence;
using InMoment.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Tests.Persistence.Repositories;

public sealed class ReactionRepositoryTests
{
    [Fact]
    public async Task AddAsync_ShouldPersistReaction()
    {
        await using var db = CreateDbContext();
        var repo = new ReactionRepository(db);

        var reaction = Reaction.Create(Guid.NewGuid(), Guid.NewGuid(), ReactionType.Heart);

        await repo.AddAsync(reaction, CancellationToken.None);
        await db.SaveChangesAsync();

        var saved = await db.Reactions.FirstOrDefaultAsync(x => x.Id == reaction.Id);
        saved.Should().NotBeNull();
        saved!.Type.Should().Be(ReactionType.Heart);
    }

    [Fact]
    public async Task GetByPhotoAndUserAsync_ShouldReturnReaction_WhenExists()
    {
        await using var db = CreateDbContext();
        var photoId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reaction = Reaction.Create(photoId, userId, ReactionType.Wow);

        db.Reactions.Add(reaction);
        await db.SaveChangesAsync();

        var repo = new ReactionRepository(db);

        var result = await repo.GetByPhotoAndUserAsync(photoId, userId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(reaction.Id);
        result.Type.Should().Be(ReactionType.Wow);
    }

    [Fact]
    public async Task GetByPhotoAndUserAsync_ShouldReturnNull_WhenMissing()
    {
        await using var db = CreateDbContext();
        var repo = new ReactionRepository(db);

        var result = await repo.GetByPhotoAndUserAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_ShouldDeleteReaction()
    {
        await using var db = CreateDbContext();
        var reaction = Reaction.Create(Guid.NewGuid(), Guid.NewGuid(), ReactionType.Laugh);

        db.Reactions.Add(reaction);
        await db.SaveChangesAsync();

        var repo = new ReactionRepository(db);

        await repo.RemoveAsync(reaction, CancellationToken.None);
        await db.SaveChangesAsync();

        var exists = await db.Reactions.AnyAsync(x => x.Id == reaction.Id);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task GetSummaryAsync_ShouldReturnCountsGroupedByReactionType()
    {
        await using var db = CreateDbContext();
        var photoId = Guid.NewGuid();

        db.Reactions.AddRange(
            Reaction.Create(photoId, Guid.NewGuid(), ReactionType.Heart),
            Reaction.Create(photoId, Guid.NewGuid(), ReactionType.Heart),
            Reaction.Create(photoId, Guid.NewGuid(), ReactionType.Wow),
            Reaction.Create(Guid.NewGuid(), Guid.NewGuid(), ReactionType.Angry)); // другое фото

        await db.SaveChangesAsync();

        var repo = new ReactionRepository(db);

        var result = await repo.GetSummaryAsync(photoId, CancellationToken.None);

        result.Should().HaveCount(2);
        result[ReactionType.Heart].Should().Be(2);
        result[ReactionType.Wow].Should().Be(1);
        result.Should().NotContainKey(ReactionType.Angry);
    }

    [Fact]
    public async Task GetSummaryAsync_ShouldReturnEmpty_WhenPhotoHasNoReactions()
    {
        await using var db = CreateDbContext();
        var repo = new ReactionRepository(db);

        var result = await repo.GetSummaryAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSummariesByPhotoIdsAsync_ShouldReturnNestedDictionary()
    {
        await using var db = CreateDbContext();
        var photo1 = Guid.NewGuid();
        var photo2 = Guid.NewGuid();

        db.Reactions.AddRange(
            Reaction.Create(photo1, Guid.NewGuid(), ReactionType.Heart),
            Reaction.Create(photo1, Guid.NewGuid(), ReactionType.Heart),
            Reaction.Create(photo1, Guid.NewGuid(), ReactionType.Wow),
            Reaction.Create(photo2, Guid.NewGuid(), ReactionType.Sad),
            Reaction.Create(photo2, Guid.NewGuid(), ReactionType.Sad),
            Reaction.Create(Guid.NewGuid(), Guid.NewGuid(), ReactionType.Angry)); // другое фото

        await db.SaveChangesAsync();

        var repo = new ReactionRepository(db);

        var result = await repo.GetSummariesByPhotoIdsAsync(new[] { photo1, photo2 }, CancellationToken.None);

        result.Should().HaveCount(2);

        result[photo1][ReactionType.Heart].Should().Be(2);
        result[photo1][ReactionType.Wow].Should().Be(1);

        result[photo2][ReactionType.Sad].Should().Be(2);
        result[photo2].Should().NotContainKey(ReactionType.Angry);
    }

    [Fact]
    public async Task GetSummariesByPhotoIdsAsync_ShouldReturnEmpty_WhenInputEmpty()
    {
        await using var db = CreateDbContext();
        var repo = new ReactionRepository(db);

        var result = await repo.GetSummariesByPhotoIdsAsync(Array.Empty<Guid>(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserReactionsByPhotoIdsAsync_ShouldReturnOnlySelectedUsersReactions()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var photo1 = Guid.NewGuid();
        var photo2 = Guid.NewGuid();
        var photo3 = Guid.NewGuid();

        db.Reactions.AddRange(
            Reaction.Create(photo1, userId, ReactionType.Heart),
            Reaction.Create(photo2, userId, ReactionType.Angry),
            Reaction.Create(photo3, otherUserId, ReactionType.Wow));

        await db.SaveChangesAsync();

        var repo = new ReactionRepository(db);

        var result = await repo.GetUserReactionsByPhotoIdsAsync(
            new[] { photo1, photo2, photo3 },
            userId,
            CancellationToken.None);

        result.Should().HaveCount(2);
        result[photo1].Should().Be(ReactionType.Heart);
        result[photo2].Should().Be(ReactionType.Angry);
        result.Should().NotContainKey(photo3);
    }

    [Fact]
    public async Task GetUserReactionsByPhotoIdsAsync_ShouldReturnEmpty_WhenInputEmpty()
    {
        await using var db = CreateDbContext();
        var repo = new ReactionRepository(db);

        var result = await repo.GetUserReactionsByPhotoIdsAsync(
            Array.Empty<Guid>(),
            Guid.NewGuid(),
            CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserReactionsByPhotoIdsAsync_ShouldReturnEmpty_WhenUserHasNoReactions()
    {
        await using var db = CreateDbContext();
        var repo = new ReactionRepository(db);

        db.Reactions.Add(
            Reaction.Create(Guid.NewGuid(), Guid.NewGuid(), ReactionType.Heart));

        await db.SaveChangesAsync();

        var result = await repo.GetUserReactionsByPhotoIdsAsync(
            new[] { Guid.NewGuid(), Guid.NewGuid() },
            Guid.NewGuid(),
            CancellationToken.None);

        result.Should().BeEmpty();
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ReactionRepositoryTests_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }
}