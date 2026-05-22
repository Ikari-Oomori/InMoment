using FluentAssertions;
using InMoment.Application.Abstractions.Accounts;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Realtime;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Notifications.Common;
using InMoment.Application.Features.Reports.ReviewReport;
using InMoment.Domain.Common;
using InMoment.Domain.Media;
using InMoment.Domain.Notifications;
using InMoment.Domain.Reports;
using Moq;

namespace InMoment.Application.Tests.Reports.ReviewReport;

public sealed class ReviewReportHandlerTests
{
    private readonly Mock<IReportRepository> _reports = new();
    private readonly Mock<IPhotoRepository> _photos = new();
    private readonly Mock<ICommentRepository> _comments = new();
    private readonly Mock<IAccountDataManager> _accounts = new();
    private readonly Mock<INotificationRepository> _notifications = new();
    private readonly Mock<INotificationRealtime> _notificationRealtime = new();
    private readonly Mock<INotificationPushDeliveryService> _pushDelivery = new();
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<ISystemModeratorAccess> _moderator = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IGroupRealtime> _realtime = new();

    private ReviewReportHandler Create()
        => new(
            _reports.Object,
            _photos.Object,
            _comments.Object,
            _accounts.Object,
            _notifications.Object,
            _notificationRealtime.Object,
            _pushDelivery.Object,
            _current.Object,
            _moderator.Object,
            _uow.Object,
            _realtime.Object);

    [Fact]
    public async Task Handle_ShouldThrow_WhenReportIdEmpty()
    {
        var handler = Create();

        var act = () => handler.Handle(
            new ReviewReportCommand(Guid.Empty, ReportStatus.Reviewed),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("ReportId is required.");
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenStatusInvalid()
    {
        _current.Setup(x => x.UserId).Returns(Guid.NewGuid());

        var handler = Create();

        var act = () => handler.Handle(
            new ReviewReportCommand(Guid.NewGuid(), (ReportStatus)999),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Некорректный статус жалобы.");
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenActionInvalid()
    {
        _current.Setup(x => x.UserId).Returns(Guid.NewGuid());

        var handler = Create();

        var act = () => handler.Handle(
            new ReviewReportCommand(Guid.NewGuid(), ReportStatus.Resolved, (ReviewReportAction)999),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Некорректное действие модерации.");
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenStatusPending()
    {
        _current.Setup(x => x.UserId).Returns(Guid.NewGuid());

        var handler = Create();

        var act = () => handler.Handle(
            new ReviewReportCommand(Guid.NewGuid(), ReportStatus.Pending),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Pending is not a valid review result.");
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenActionUsedWithNonResolvedStatus()
    {
        _current.Setup(x => x.UserId).Returns(Guid.NewGuid());

        var handler = Create();

        var act = () => handler.Handle(
            new ReviewReportCommand(Guid.NewGuid(), ReportStatus.Reviewed, ReviewReportAction.DeletePhoto),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Действие модерации допустимо только со статусом Resolved.");
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenActionDoesNotMatchTargetType()
    {
        var moderatorId = Guid.NewGuid();

        var report = Report.Create(
            Guid.NewGuid(),
            ReportTargetType.Photo,
            Guid.NewGuid(),
            ReportReason.Spam,
            null);

        _current.Setup(x => x.UserId).Returns(moderatorId);
        _reports.Setup(x => x.GetByIdAsync(report.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        var handler = Create();

        var act = () => handler.Handle(
            new ReviewReportCommand(report.Id, ReportStatus.Resolved, ReviewReportAction.DeleteComment),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Выбранное действие модерации не соответствует типу жалобы.");
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenNotModerator()
    {
        var currentUserId = Guid.NewGuid();
        _current.Setup(x => x.UserId).Returns(currentUserId);

        _moderator.Setup(x => x.EnsureModerator(currentUserId))
            .Throws(new ForbiddenException("Доступ разрешён только системному модератору."));

        var handler = Create();

        var act = () => handler.Handle(
            new ReviewReportCommand(Guid.NewGuid(), ReportStatus.Rejected),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Доступ разрешён только системному модератору.");

        _reports.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenReportNotFound()
    {
        var currentUserId = Guid.NewGuid();

        _current.Setup(x => x.UserId).Returns(currentUserId);
        _reports.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Report?)null);

        var handler = Create();

        var act = () => handler.Handle(
            new ReviewReportCommand(Guid.NewGuid(), ReportStatus.Reviewed),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Жалоба не найдена.");
    }

    [Fact]
    public async Task Handle_ShouldReviewReport_WithoutAction()
    {
        var moderatorId = Guid.NewGuid();

        var report = Report.Create(
            Guid.NewGuid(),
            ReportTargetType.Photo,
            Guid.NewGuid(),
            ReportReason.Spam,
            null);

        _current.Setup(x => x.UserId).Returns(moderatorId);
        _reports.Setup(x => x.GetByIdAsync(report.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _notifications.Setup(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _notifications.Setup(x => x.GetUnreadCountAsync(report.ReporterUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var handler = Create();

        var id = await handler.Handle(
            new ReviewReportCommand(report.Id, ReportStatus.Reviewed),
            CancellationToken.None);

        id.Should().Be(report.Id);
        report.Status.Should().Be(ReportStatus.Reviewed);
        report.ReviewedByUserId.Should().Be(moderatorId);
        report.ReviewedAt.Should().NotBeNull();

        _notifications.Verify(x => x.AddAsync(
            It.Is<Notification>(n =>
                n.UserId == report.ReporterUserId &&
                n.Type == NotificationType.ReportReviewed),
            It.IsAny<CancellationToken>()), Times.Once);

        _notificationRealtime.Verify(x => x.NotifyNotificationsChangedAsync(
            report.ReporterUserId,
            3,
            It.IsAny<CancellationToken>()), Times.Once);

        _pushDelivery.Verify(x => x.TrySendAsync(
            It.Is<Notification>(n =>
                n.UserId == report.ReporterUserId &&
                n.Type == NotificationType.ReportReviewed),
            It.IsAny<CancellationToken>()), Times.Once);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldResolveReport_AndDeletePhoto()
    {
        var moderatorId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var report = Report.Create(
            Guid.NewGuid(),
            ReportTargetType.Photo,
            Guid.NewGuid(),
            ReportReason.Spam,
            null);

        var photo = Photo.Create(
            groupId,
            Guid.NewGuid(),
            "groups/test/photos/p1.jpg",
            "image/jpeg",
            1024);

        typeof(Photo).GetProperty(nameof(Photo.Id))!.SetValue(photo, report.TargetId);

        _current.Setup(x => x.UserId).Returns(moderatorId);
        _reports.Setup(x => x.GetByIdAsync(report.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _photos.Setup(x => x.GetByIdAsync(report.TargetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _notifications.Setup(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _notifications.Setup(x => x.GetUnreadCountAsync(report.ReporterUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var handler = Create();

        var id = await handler.Handle(
            new ReviewReportCommand(report.Id, ReportStatus.Resolved, ReviewReportAction.DeletePhoto),
            CancellationToken.None);

        id.Should().Be(report.Id);
        photo.IsDeleted.Should().BeTrue();
        report.Status.Should().Be(ReportStatus.Resolved);

        _notifications.Verify(x => x.AddAsync(
            It.Is<Notification>(n =>
                n.UserId == report.ReporterUserId &&
                n.Type == NotificationType.ReportReviewed),
            It.IsAny<CancellationToken>()), Times.Once);

        _notificationRealtime.Verify(x => x.NotifyNotificationsChangedAsync(
            report.ReporterUserId,
            2,
            It.IsAny<CancellationToken>()), Times.Once);

        _pushDelivery.Verify(x => x.TrySendAsync(
            It.Is<Notification>(n =>
                n.UserId == report.ReporterUserId &&
                n.Type == NotificationType.ReportReviewed),
            It.IsAny<CancellationToken>()), Times.Once);

        _realtime.Verify(x => x.NotifyFeedChangedAsync(
            groupId,
            "photo_deleted",
            photo.Id,
            It.IsAny<CancellationToken>()), Times.Once);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldResolveReport_AndDeleteComment()
    {
        var moderatorId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var report = Report.Create(
            Guid.NewGuid(),
            ReportTargetType.Comment,
            Guid.NewGuid(),
            ReportReason.Harassment,
            null);

        var photo = Photo.Create(
            groupId,
            Guid.NewGuid(),
            "groups/test/photos/p1.jpg",
            "image/jpeg",
            1024);

        var comment = Comment.CreateRoot(photo.Id, Guid.NewGuid(), "bad comment");
        typeof(Comment).GetProperty(nameof(Comment.Id))!.SetValue(comment, report.TargetId);

        _current.Setup(x => x.UserId).Returns(moderatorId);
        _reports.Setup(x => x.GetByIdAsync(report.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _comments.Setup(x => x.GetByIdAsync(report.TargetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);
        _photos.Setup(x => x.GetByIdAsync(photo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);
        _notifications.Setup(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _notifications.Setup(x => x.GetUnreadCountAsync(report.ReporterUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var handler = Create();

        var id = await handler.Handle(
            new ReviewReportCommand(report.Id, ReportStatus.Resolved, ReviewReportAction.DeleteComment),
            CancellationToken.None);

        id.Should().Be(report.Id);
        comment.IsDeleted.Should().BeTrue();
        report.Status.Should().Be(ReportStatus.Resolved);

        _notifications.Verify(x => x.AddAsync(
            It.Is<Notification>(n =>
                n.UserId == report.ReporterUserId &&
                n.Type == NotificationType.ReportReviewed),
            It.IsAny<CancellationToken>()), Times.Once);

        _notificationRealtime.Verify(x => x.NotifyNotificationsChangedAsync(
            report.ReporterUserId,
            5,
            It.IsAny<CancellationToken>()), Times.Once);

        _pushDelivery.Verify(x => x.TrySendAsync(
            It.Is<Notification>(n =>
                n.UserId == report.ReporterUserId &&
                n.Type == NotificationType.ReportReviewed),
            It.IsAny<CancellationToken>()), Times.Once);

        _realtime.Verify(x => x.NotifyFeedChangedAsync(
            groupId,
            "comment_changed",
            photo.Id,
            It.IsAny<CancellationToken>()), Times.Once);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldResolveReport_AndDeactivateUser()
    {
        var moderatorId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var report = Report.Create(
            Guid.NewGuid(),
            ReportTargetType.User,
            targetUserId,
            ReportReason.Other,
            null);

        _current.Setup(x => x.UserId).Returns(moderatorId);
        _reports.Setup(x => x.GetByIdAsync(report.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _notifications.Setup(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _notifications.Setup(x => x.GetUnreadCountAsync(report.ReporterUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = Create();

        var id = await handler.Handle(
            new ReviewReportCommand(report.Id, ReportStatus.Resolved, ReviewReportAction.DeactivateUser),
            CancellationToken.None);

        id.Should().Be(report.Id);
        report.Status.Should().Be(ReportStatus.Resolved);

        _accounts.Verify(x => x.DeactivateAccountAsync(targetUserId, It.IsAny<CancellationToken>()), Times.Once);

        _notifications.Verify(x => x.AddAsync(
            It.Is<Notification>(n =>
                n.UserId == report.ReporterUserId &&
                n.Type == NotificationType.ReportReviewed),
            It.IsAny<CancellationToken>()), Times.Once);

        _notificationRealtime.Verify(x => x.NotifyNotificationsChangedAsync(
            report.ReporterUserId,
            1,
            It.IsAny<CancellationToken>()), Times.Once);

        _pushDelivery.Verify(x => x.TrySendAsync(
            It.Is<Notification>(n =>
                n.UserId == report.ReporterUserId &&
                n.Type == NotificationType.ReportReviewed),
            It.IsAny<CancellationToken>()), Times.Once);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldSendAppealReviewedNotification_WhenReportHasAppeal()
    {
        var moderatorId = Guid.NewGuid();
        var firstModeratorId = Guid.NewGuid();

        var report = Report.Create(
            Guid.NewGuid(),
            ReportTargetType.Photo,
            Guid.NewGuid(),
            ReportReason.Spam,
            null);

        report.MarkReviewed(
            firstModeratorId,
            ReportStatus.Reviewed,
            ReportDecisionAction.None);

        report.SubmitAppeal(report.ReporterUserId, "Прошу пересмотреть жалобу.");

        _current.Setup(x => x.UserId).Returns(moderatorId);
        _reports.Setup(x => x.GetByIdAsync(report.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _notifications.Setup(x => x.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _notifications.Setup(x => x.GetUnreadCountAsync(report.ReporterUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        var handler = Create();

        var id = await handler.Handle(
            new ReviewReportCommand(report.Id, ReportStatus.Reviewed),
            CancellationToken.None);

        id.Should().Be(report.Id);

        _notifications.Verify(x => x.AddAsync(
            It.Is<Notification>(n =>
                n.UserId == report.ReporterUserId &&
                n.Type == NotificationType.ReportAppealReviewed),
            It.IsAny<CancellationToken>()), Times.Once);

        _notificationRealtime.Verify(x => x.NotifyNotificationsChangedAsync(
            report.ReporterUserId,
            4,
            It.IsAny<CancellationToken>()), Times.Once);

        _pushDelivery.Verify(x => x.TrySendAsync(
            It.Is<Notification>(n =>
                n.UserId == report.ReporterUserId &&
                n.Type == NotificationType.ReportAppealReviewed),
            It.IsAny<CancellationToken>()), Times.Once);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}