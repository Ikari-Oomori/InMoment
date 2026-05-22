using FluentAssertions;
using InMoment.Domain.Reports;
using InMoment.Infrastructure.Persistence;
using InMoment.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InMoment.Infrastructure.Tests.Persistence.Repositories;

public sealed class ReportRepositoryTests
{
    [Fact]
    public async Task AddAsync_ShouldPersist()
    {
        await using var db = CreateDb();
        var repo = new ReportRepository(db);

        var report = CreateReport();

        await repo.AddAsync(report, CancellationToken.None);
        await db.SaveChangesAsync();

        var saved = await db.Set<Report>().FirstOrDefaultAsync();

        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnReport()
    {
        await using var db = CreateDb();

        var report = CreateReport();
        db.Add(report);
        await db.SaveChangesAsync();

        var repo = new ReportRepository(db);

        var result = await repo.GetByIdAsync(report.Id, CancellationToken.None);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByReporterAsync_ShouldReturnOrdered()
    {
        await using var db = CreateDb();

        var userId = Guid.NewGuid();

        var a = CreateReport(userId);
        var b = CreateReport(userId);
        var c = CreateReport(userId);

        SetCreatedAt(a, new DateTime(2026, 1, 1));
        SetCreatedAt(b, new DateTime(2026, 1, 2));
        SetCreatedAt(c, new DateTime(2026, 1, 3));

        db.AddRange(a, b, c);
        await db.SaveChangesAsync();

        var repo = new ReportRepository(db);

        var result = await repo.GetByReporterAsync(userId, 10, CancellationToken.None);

        result.Select(x => x.Id).Should().ContainInOrder(c.Id, b.Id, a.Id);
    }

    [Fact]
    public async Task ExistsSimilarPendingAsync_ShouldReturnTrue_WhenExists()
    {
        await using var db = CreateDb();

        var userId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var report = Report.Create(
            userId,
            ReportTargetType.Photo,
            targetId,
            ReportReason.Spam,
            null);

        db.Add(report);
        await db.SaveChangesAsync();

        var repo = new ReportRepository(db);

        var result = await repo.ExistsSimilarPendingAsync(
            userId,
            ReportTargetType.Photo,
            targetId,
            CancellationToken.None);

        result.Should().BeTrue();
    }

    private static Report CreateReport(Guid? userId = null)
    {
        return Report.Create(
            userId ?? Guid.NewGuid(),
            ReportTargetType.Photo,
            Guid.NewGuid(),
            ReportReason.Spam,
            null);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static void SetCreatedAt(Report r, DateTime dt)
    {
        typeof(Report)
            .GetProperty(nameof(Report.CreatedAt))!
            .SetValue(r, dt);
    }
}