using FluentAssertions;
using InMoment.Domain.Common;
using InMoment.Domain.Reports;

namespace InMoment.Domain.Tests.Reports;

public sealed class ReportTests
{
    [Fact]
    public void Create_ShouldThrow_WhenReporterEmpty()
    {
        var act = () => Report.Create(
            Guid.Empty,
            ReportTargetType.Photo,
            Guid.NewGuid(),
            ReportReason.Spam,
            null);

        act.Should().Throw<ValidationException>()
            .WithMessage("ReporterUserId is required.");
    }

    [Fact]
    public void Create_ShouldThrow_WhenTargetEmpty()
    {
        var act = () => Report.Create(
            Guid.NewGuid(),
            ReportTargetType.Photo,
            Guid.Empty,
            ReportReason.Spam,
            null);

        act.Should().Throw<ValidationException>()
            .WithMessage("TargetId is required.");
    }

    [Fact]
    public void Create_ShouldTrimDescription()
    {
        var report = Report.Create(
            Guid.NewGuid(),
            ReportTargetType.Photo,
            Guid.NewGuid(),
            ReportReason.Spam,
            "   test   ");

        report.Description.Should().Be("test");
    }

    [Fact]
    public void Create_ShouldThrow_WhenDescriptionTooLong()
    {
        var longText = new string('a', 501);

        var act = () => Report.Create(
            Guid.NewGuid(),
            ReportTargetType.Photo,
            Guid.NewGuid(),
            ReportReason.Spam,
            longText);

        act.Should().Throw<ValidationException>()
            .WithMessage("Description must be 500 characters or less.");
    }

    [Fact]
    public void MarkReviewed_ShouldWork()
    {
        var report = Report.Create(
            Guid.NewGuid(),
            ReportTargetType.Photo,
            Guid.NewGuid(),
            ReportReason.Spam,
            null);

        var reviewer = Guid.NewGuid();

        report.MarkReviewed(reviewer, ReportStatus.Reviewed);

        report.Status.Should().Be(ReportStatus.Reviewed);
        report.ReviewedByUserId.Should().Be(reviewer);
        report.ReviewedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkReviewed_ShouldThrow_WhenAlreadyReviewed()
    {
        var report = Report.Create(
            Guid.NewGuid(),
            ReportTargetType.Photo,
            Guid.NewGuid(),
            ReportReason.Spam,
            null);

        report.MarkReviewed(Guid.NewGuid(), ReportStatus.Reviewed);

        var act = () => report.MarkReviewed(Guid.NewGuid(), ReportStatus.Resolved);

        act.Should().Throw<ValidationException>()
            .WithMessage("Report has already been reviewed.");
    }

    [Fact]
    public void MarkReviewed_ShouldThrow_WhenPendingStatus()
    {
        var report = Report.Create(
            Guid.NewGuid(),
            ReportTargetType.Photo,
            Guid.NewGuid(),
            ReportReason.Spam,
            null);

        var act = () => report.MarkReviewed(Guid.NewGuid(), ReportStatus.Pending);

        act.Should().Throw<ValidationException>()
            .WithMessage("Pending is not a valid review result.");
    }
}