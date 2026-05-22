using InMoment.Domain.Media;
using InMoment.Infrastructure.Persistence;
using InMoment.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Tests.Persistence.Repositories;

public sealed class CommentRepositoryTests
{
    [Fact]
    public async Task AddAsync_ShouldPersistComment()
    {
        await using var db = CreateDbContext();
        var repo = new CommentRepository(db);

        var comment = Comment.CreateRoot(Guid.NewGuid(), Guid.NewGuid(), "hello");

        await repo.AddAsync(comment, CancellationToken.None);
        await db.SaveChangesAsync();

        var saved = await db.Comments.FirstOrDefaultAsync(x => x.Id == comment.Id);
        saved.Should().NotBeNull();
        saved!.Text.Should().Be("hello");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnComment_WhenExists()
    {
        await using var db = CreateDbContext();
        var comment = Comment.CreateRoot(Guid.NewGuid(), Guid.NewGuid(), "hello");
        db.Comments.Add(comment);
        await db.SaveChangesAsync();

        var repo = new CommentRepository(db);

        var result = await repo.GetByIdAsync(comment.Id, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(comment.Id);
    }

    [Fact]
    public async Task GetByPhotoAsync_ShouldReturnOnlyNonDeletedComments_OrderedAscending()
    {
        await using var db = CreateDbContext();
        var photoId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var first = Comment.CreateRoot(photoId, userId, "first");
        var second = Comment.CreateRoot(photoId, userId, "second");
        var deleted = Comment.CreateRoot(photoId, userId, "deleted");

        SetCreatedAt(first, new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(second, new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc));
        MarkDeleted(deleted, userId);

        db.Comments.AddRange(first, second, deleted);
        await db.SaveChangesAsync();

        var repo = new CommentRepository(db);

        var result = await repo.GetByPhotoAsync(photoId, 10, CancellationToken.None);

        result.Select(x => x.Id).Should().ContainInOrder(first.Id, second.Id);
        result.Should().NotContain(x => x.IsDeleted);
    }

    [Fact]
    public async Task GetByPhotoAsync_ShouldRespectLimit()
    {
        await using var db = CreateDbContext();
        var photoId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var first = Comment.CreateRoot(photoId, userId, "first");
        var second = Comment.CreateRoot(photoId, userId, "second");
        var third = Comment.CreateRoot(photoId, userId, "third");

        SetCreatedAt(first, new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(second, new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(third, new DateTime(2026, 1, 3, 10, 0, 0, DateTimeKind.Utc));

        db.Comments.AddRange(first, second, third);
        await db.SaveChangesAsync();

        var repo = new CommentRepository(db);

        var result = await repo.GetByPhotoAsync(photoId, 2, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(x => x.Id).Should().ContainInOrder(first.Id, second.Id);
    }

    [Fact]
    public async Task GetCountsByPhotoIdsAsync_ShouldReturnCounts_ExcludingDeleted()
    {
        await using var db = CreateDbContext();
        var photo1 = Guid.NewGuid();
        var photo2 = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var c1 = Comment.CreateRoot(photo1, userId, "1");
        var c2 = Comment.CreateRoot(photo1, userId, "2");
        var c3 = Comment.CreateRoot(photo2, userId, "3");
        var deleted = Comment.CreateRoot(photo2, userId, "deleted");
        MarkDeleted(deleted, userId);

        db.Comments.AddRange(c1, c2, c3, deleted);
        await db.SaveChangesAsync();

        var repo = new CommentRepository(db);

        var result = await repo.GetCountsByPhotoIdsAsync(new[] { photo1, photo2 }, CancellationToken.None);

        result.Should().HaveCount(2);
        result[photo1].Should().Be(2);
        result[photo2].Should().Be(1);
    }

    [Fact]
    public async Task GetCountsByPhotoIdsAsync_ShouldReturnEmpty_WhenInputEmpty()
    {
        await using var db = CreateDbContext();
        var repo = new CommentRepository(db);

        var result = await repo.GetCountsByPhotoIdsAsync(Array.Empty<Guid>(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPageByPhotoAsync_ShouldReturnLatestPage_WhenCursorNotProvided()
    {
        await using var db = CreateDbContext();
        var photoId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var c1 = Comment.CreateRoot(photoId, userId, "1");
        var c2 = Comment.CreateRoot(photoId, userId, "2");
        var c3 = Comment.CreateRoot(photoId, userId, "3");

        SetCreatedAt(c1, new DateTime(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(c2, new DateTime(2026, 2, 2, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(c3, new DateTime(2026, 2, 3, 10, 0, 0, DateTimeKind.Utc));

        db.Comments.AddRange(c1, c2, c3);
        await db.SaveChangesAsync();

        var repo = new CommentRepository(db);

        var result = await repo.GetPageByPhotoAsync(photoId, 2, null, null, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(x => x.Id).Should().ContainInOrder(c3.Id, c2.Id);
    }

    [Fact]
    public async Task GetPageByPhotoAsync_ShouldApplyCursor_ByCreatedAtAndId()
    {
        await using var db = CreateDbContext();
        var photoId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ts = new DateTime(2026, 2, 10, 10, 0, 0, DateTimeKind.Utc);

        var older = Comment.CreateRoot(photoId, userId, "older");
        var sameTimeLowerId = Comment.CreateRoot(photoId, userId, "same-low");
        var cursor = Comment.CreateRoot(photoId, userId, "cursor");
        var newer = Comment.CreateRoot(photoId, userId, "newer");

        SetCreatedAt(older, ts.AddDays(-1));
        SetCreatedAt(sameTimeLowerId, ts);
        SetCreatedAt(cursor, ts);
        SetCreatedAt(newer, ts.AddDays(1));

        if (sameTimeLowerId.Id.CompareTo(cursor.Id) > 0)
        {
            (sameTimeLowerId, cursor) = (cursor, sameTimeLowerId);
            SetText(sameTimeLowerId, "same-low");
            SetText(cursor, "cursor");
        }

        db.Comments.AddRange(older, sameTimeLowerId, cursor, newer);
        await db.SaveChangesAsync();

        var repo = new CommentRepository(db);

        var result = await repo.GetPageByPhotoAsync(
            photoId,
            10,
            cursor.CreatedAt,
            cursor.Id,
            CancellationToken.None);

        result.Select(x => x.Id).Should().ContainInOrder(sameTimeLowerId.Id, older.Id);
        result.Should().NotContain(x => x.Id == cursor.Id);
        result.Should().NotContain(x => x.Id == newer.Id);
    }

    [Fact]
    public async Task GetLatestByPhotoIdsAsync_ShouldReturnLatestCommentPerPhoto_ExcludingDeleted()
    {
        await using var db = CreateDbContext();
        var photo1 = Guid.NewGuid();
        var photo2 = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var p1Old = Comment.CreateRoot(photo1, userId, "p1-old");
        var p1Latest = Comment.CreateRoot(photo1, userId, "p1-latest");
        var p2Latest = Comment.CreateRoot(photo2, userId, "p2-latest");
        var p2DeletedNewest = Comment.CreateRoot(photo2, userId, "p2-deleted");

        SetCreatedAt(p1Old, new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(p1Latest, new DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(p2Latest, new DateTime(2026, 3, 3, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(p2DeletedNewest, new DateTime(2026, 3, 4, 10, 0, 0, DateTimeKind.Utc));
        MarkDeleted(p2DeletedNewest, userId);

        db.Comments.AddRange(p1Old, p1Latest, p2Latest, p2DeletedNewest);
        await db.SaveChangesAsync();

        var repo = new CommentRepository(db);

        var result = await repo.GetLatestByPhotoIdsAsync(new[] { photo1, photo2 }, CancellationToken.None);

        result.Should().HaveCount(2);
        result[photo1].Id.Should().Be(p1Latest.Id);
        result[photo2].Id.Should().Be(p2Latest.Id);
    }

    [Fact]
    public async Task GetLatestByPhotoIdsAsync_ShouldReturnEmpty_WhenInputEmpty()
    {
        await using var db = CreateDbContext();
        var repo = new CommentRepository(db);

        var result = await repo.GetLatestByPhotoIdsAsync(Array.Empty<Guid>(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByPhotoAsync_ShouldReturnRootAndReplyComments_WhenNotDeleted()
    {
        await using var db = CreateDbContext();
        var photoId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var root = Comment.CreateRoot(photoId, userId, "root");
        var reply = Comment.CreateReply(photoId, userId, root.Id, "reply");

        SetCreatedAt(root, new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        SetCreatedAt(reply, new DateTime(2026, 4, 1, 11, 0, 0, DateTimeKind.Utc));

        db.Comments.AddRange(root, reply);
        await db.SaveChangesAsync();

        var repo = new CommentRepository(db);

        var result = await repo.GetByPhotoAsync(photoId, 10, CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].ParentCommentId.Should().BeNull();
        result[1].ParentCommentId.Should().Be(root.Id);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CommentRepositoryTests_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static void SetCreatedAt(Comment comment, DateTime createdAtUtc)
    {
        typeof(Comment)
            .GetProperty(nameof(Comment.CreatedAt))!
            .SetValue(comment, createdAtUtc);
    }

    private static void SetText(Comment comment, string text)
    {
        typeof(Comment)
            .GetProperty(nameof(Comment.Text))!
            .SetValue(comment, text);
    }

    private static void MarkDeleted(Comment comment, Guid actorUserId)
    {
        comment.Delete(actorUserId);
    }
}