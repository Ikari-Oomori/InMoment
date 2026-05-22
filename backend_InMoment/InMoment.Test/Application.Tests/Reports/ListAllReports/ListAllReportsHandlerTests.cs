using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Abstractions.Storage;
using InMoment.Application.Features.Reports.Common;
using InMoment.Application.Features.Reports.ListAllReports;
using InMoment.Domain.Reports;
using InMoment.Domain.Users;
using Moq;

namespace InMoment.Application.Tests.Features.Reports.ListAllReports;

public sealed class ListAllReportsHandlerTests
{
    private readonly Mock<IReportRepository> _reports = new();
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<ISystemModeratorAccess> _moderatorAccess = new();

    private readonly Mock<IPhotoRepository> _photos = new();
    private readonly Mock<ICommentRepository> _comments = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IFileStorage> _storage = new();

    private ListAllReportsHandler Create()
        => new(
            _reports.Object,
            _current.Object,
            _moderatorAccess.Object,
            new ReportTargetContextFactory(
                _photos.Object,
                _comments.Object,
                _users.Object,
                _groups.Object,
                _storage.Object,
                _reports.Object),
            new ReportDtoBuilders(_users.Object));

    [Fact]
    public async Task Handle_ShouldUseDefaultLimit_WhenLimitTooSmall()
    {
        _current.Setup(x => x.UserId).Returns(Guid.NewGuid());

        _reports.Setup(x => x.GetAllFilteredAsync(
                100,
                null,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Report>());

        var handler = Create();

        await handler.Handle(new ListAllReportsQuery(0), CancellationToken.None);

        _moderatorAccess.Verify(x => x.EnsureModerator(It.IsAny<Guid>()), Times.Once);
        _reports.Verify(x => x.GetAllFilteredAsync(
            100,
            null,
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenNoReports()
    {
        _current.Setup(x => x.UserId).Returns(Guid.NewGuid());

        _reports.Setup(x => x.GetAllFilteredAsync(
                100,
                null,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Report>());

        var handler = Create();

        var result = await handler.Handle(new ListAllReportsQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldMapReportsToDto()
    {
        _current.Setup(x => x.UserId).Returns(Guid.NewGuid());

        var report1 = Report.Create(
            Guid.NewGuid(),
            ReportTargetType.Photo,
            Guid.NewGuid(),
            ReportReason.Spam,
            "spam");

        var report2 = Report.Create(
            Guid.NewGuid(),
            ReportTargetType.Comment,
            Guid.NewGuid(),
            ReportReason.Harassment,
            "bad");

        var reviewerId = Guid.NewGuid();
        report2.MarkReviewed(reviewerId, ReportStatus.Reviewed);

        _reports.Setup(x => x.GetAllFilteredAsync(
                100,
                null,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { report1, report2 });

        _users.Setup(x => x.GetByIdAsync(report1.ReporterUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser(report1.ReporterUserId, "reporter_one"));

        _users.Setup(x => x.GetByIdAsync(report2.ReporterUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser(report2.ReporterUserId, "reporter_two"));

        var handler = Create();

        var result = await handler.Handle(new ListAllReportsQuery(), CancellationToken.None);

        result.Should().HaveCount(2);

        result[0].Id.Should().Be(report1.Id);
        result[0].Reporter.Should().NotBeNull();
        result[0].Resolution.Should().NotBeNull();

        result[1].Id.Should().Be(report2.Id);
        result[1].Reporter.Should().NotBeNull();
        result[1].Resolution.Should().NotBeNull();
    }

    private static User CreateUser(Guid id, string userName)
    {
        var user = User.Create(
            email: $"{userName}@test.com",
            passwordHash: "hash",
            userName: userName,
            firstName: "Test",
            lastName: "User");

        typeof(User)
            .GetProperty(nameof(User.Id))!
            .SetValue(user, id);

        return user;
    }
}