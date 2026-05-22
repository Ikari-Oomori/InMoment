using InMoment.Application.Abstractions.Accounts;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Realtime;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Notifications.Common;
using InMoment.Domain.Common;
using InMoment.Domain.Notifications;
using InMoment.Domain.Reports;
using MediatR;

namespace InMoment.Application.Features.Reports.ReviewReport;

public sealed class ReviewReportHandler : IRequestHandler<ReviewReportCommand, Guid>
{
    private readonly IReportRepository _reports;
    private readonly IPhotoRepository _photos;
    private readonly ICommentRepository _comments;
    private readonly IAccountDataManager _accounts;
    private readonly INotificationRepository _notifications;
    private readonly INotificationRealtime _notificationRealtime;
    private readonly INotificationPushDeliveryService _pushDelivery;
    private readonly ICurrentUser _current;
    private readonly ISystemModeratorAccess _moderatorAccess;
    private readonly IUnitOfWork _uow;
    private readonly IGroupRealtime _realtime;

    public ReviewReportHandler(
        IReportRepository reports,
        IPhotoRepository photos,
        ICommentRepository comments,
        IAccountDataManager accounts,
        INotificationRepository notifications,
        INotificationRealtime notificationRealtime,
        INotificationPushDeliveryService pushDelivery,
        ICurrentUser current,
        ISystemModeratorAccess moderatorAccess,
        IUnitOfWork uow,
        IGroupRealtime realtime)
    {
        _reports = reports;
        _photos = photos;
        _comments = comments;
        _accounts = accounts;
        _notifications = notifications;
        _notificationRealtime = notificationRealtime;
        _pushDelivery = pushDelivery;
        _current = current;
        _moderatorAccess = moderatorAccess;
        _uow = uow;
        _realtime = realtime;
    }

    public async Task<Guid> Handle(ReviewReportCommand cmd, CancellationToken ct)
    {
        if (cmd.ReportId == Guid.Empty)
            throw new ValidationException("ReportId is required.");

        if (!Enum.IsDefined(typeof(ReportStatus), cmd.Status))
            throw new ValidationException("Некорректный статус жалобы.");

        if (!Enum.IsDefined(typeof(ReviewReportAction), cmd.Action))
            throw new ValidationException("Некорректное действие модерации.");

        if (cmd.Status == ReportStatus.Pending)
            throw new ValidationException("Pending is not a valid review result.");

        if (cmd.Action != ReviewReportAction.None && cmd.Status != ReportStatus.Resolved)
            throw new ValidationException("Действие модерации допустимо только со статусом Resolved.");

        _moderatorAccess.EnsureModerator(_current.UserId);

        var report = await _reports.GetByIdAsync(cmd.ReportId, ct)
                     ?? throw new NotFoundException("Жалоба не найдена.");

        var hadAppealBeforeReview = !string.IsNullOrWhiteSpace(report.AppealText);

        ValidateActionCompatibility(report.TargetType, cmd.Action);

        await ApplyActionAsync(report, cmd.Action, ct);

        report.MarkReviewed(
            _current.UserId,
            cmd.Status,
            MapDecisionAction(cmd.Action));

        var notification = hadAppealBeforeReview
            ? Notification.CreateReportAppealReviewed(
                userId: report.ReporterUserId,
                actorUserId: _current.UserId)
            : Notification.CreateReportReviewed(
                userId: report.ReporterUserId,
                actorUserId: _current.UserId);

        await _notifications.AddAsync(notification, ct);
        await _uow.SaveChangesAsync(ct);

        var unreadCount = await _notifications.GetUnreadCountAsync(report.ReporterUserId, ct);
        await _notificationRealtime.NotifyNotificationsChangedAsync(report.ReporterUserId, unreadCount, ct);
        await _pushDelivery.TrySendAsync(notification, ct);

        return report.Id;
    }

    private static void ValidateActionCompatibility(
        ReportTargetType targetType,
        ReviewReportAction action)
    {
        if (action == ReviewReportAction.None)
            return;

        var valid = targetType switch
        {
            ReportTargetType.Photo => action == ReviewReportAction.DeletePhoto,
            ReportTargetType.Comment => action == ReviewReportAction.DeleteComment,
            ReportTargetType.User => action == ReviewReportAction.DeactivateUser,
            _ => false
        };

        if (!valid)
            throw new ValidationException("Выбранное действие модерации не соответствует типу жалобы.");
    }

    private async Task ApplyActionAsync(Report report, ReviewReportAction action, CancellationToken ct)
    {
        switch (action)
        {
            case ReviewReportAction.None:
                return;

            case ReviewReportAction.DeletePhoto:
                {
                    var photo = await _photos.GetByIdAsync(report.TargetId, ct)
                                ?? throw new NotFoundException("Фото не найдено.");

                    if (!photo.IsDeleted)
                    {
                        photo.MarkDeletedByModerator();

                        await _realtime.NotifyFeedChangedAsync(
                            photo.GroupId,
                            reason: "photo_deleted",
                            photoId: photo.Id,
                            ct);
                    }

                    return;
                }

            case ReviewReportAction.DeleteComment:
                {
                    var comment = await _comments.GetByIdAsync(report.TargetId, ct)
                                  ?? throw new NotFoundException("Комментарий не найден.");

                    if (!comment.IsDeleted)
                        comment.DeleteAsOwner(_current.UserId);

                    var photo = await _photos.GetByIdAsync(comment.PhotoId, ct);
                    if (photo is not null && !photo.IsDeleted)
                    {
                        await _realtime.NotifyFeedChangedAsync(
                            photo.GroupId,
                            reason: "comment_changed",
                            photoId: photo.Id,
                            ct);
                    }

                    return;
                }

            case ReviewReportAction.DeactivateUser:
                {
                    await _accounts.DeactivateAccountAsync(report.TargetId, ct);
                    return;
                }

            default:
                throw new ValidationException("Некорректное действие модерации.");
        }
    }

    private static ReportDecisionAction MapDecisionAction(ReviewReportAction action)
    {
        return action switch
        {
            ReviewReportAction.None => ReportDecisionAction.None,
            ReviewReportAction.DeletePhoto => ReportDecisionAction.DeletePhoto,
            ReviewReportAction.DeleteComment => ReportDecisionAction.DeleteComment,
            ReviewReportAction.DeactivateUser => ReportDecisionAction.DeactivateUser,
            _ => ReportDecisionAction.None
        };
    }
}