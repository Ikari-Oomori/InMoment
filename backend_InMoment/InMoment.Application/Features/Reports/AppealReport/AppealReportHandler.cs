using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Realtime;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Notifications.Common;
using InMoment.Domain.Common;
using InMoment.Domain.Notifications;
using MediatR;

namespace InMoment.Application.Features.Reports.AppealReport;

public sealed class AppealReportHandler : IRequestHandler<AppealReportCommand, Guid>
{
    private readonly IReportRepository _reports;
    private readonly INotificationRepository _notifications;
    private readonly INotificationRealtime _notificationRealtime;
    private readonly INotificationPushDeliveryService _pushDelivery;
    private readonly ICurrentUser _current;
    private readonly IUnitOfWork _uow;

    public AppealReportHandler(
        IReportRepository reports,
        INotificationRepository notifications,
        INotificationRealtime notificationRealtime,
        INotificationPushDeliveryService pushDelivery,
        ICurrentUser current,
        IUnitOfWork uow)
    {
        _reports = reports;
        _notifications = notifications;
        _notificationRealtime = notificationRealtime;
        _pushDelivery = pushDelivery;
        _current = current;
        _uow = uow;
    }

    public async Task<Guid> Handle(AppealReportCommand cmd, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        if (cmd.ReportId == Guid.Empty)
            throw new ValidationException("ReportId is required.");

        var report = await _reports.GetByIdAsync(cmd.ReportId, ct)
                     ?? throw new NotFoundException("Report not found.");

        if (report.ReporterUserId != _current.UserId)
            throw new ForbiddenException("Можно обжаловать только свою жалобу.");

        report.SubmitAppeal(_current.UserId, cmd.Text);

        var notification = Notification.CreateReportAppealSubmitted(
            userId: report.ReporterUserId,
            actorUserId: _current.UserId);

        await _notifications.AddAsync(notification, ct);
        await _uow.SaveChangesAsync(ct);

        var unreadCount = await _notifications.GetUnreadCountAsync(report.ReporterUserId, ct);
        await _notificationRealtime.NotifyNotificationsChangedAsync(report.ReporterUserId, unreadCount, ct);
        await _pushDelivery.TrySendAsync(notification, ct);

        return report.Id;
    }
}