using InMoment.Application.Abstractions.Persistence;
using InMoment.Domain.Reports;
using InMoment.Domain.Users;

namespace InMoment.Application.Features.Reports.Common;

public sealed class ReportDtoBuilders
{
    private readonly IUserRepository _users;

    public ReportDtoBuilders(IUserRepository users)
    {
        _users = users;
    }

    public async Task<ReporterPreviewDto?> BuildReporterAsync(Guid reporterUserId, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(reporterUserId, ct);
        if (user is null)
            return null;

        return new ReporterPreviewDto(
            user.Id,
            user.UserName,
            BuildDisplayName(user),
            user.ProfilePhotoUrl
        );
    }

    public ReportResolutionInfoDto BuildResolution(Report report)
    {
        return report.Status switch
        {
            ReportStatus.Pending when !string.IsNullOrWhiteSpace(report.AppealText)
                => new ReportResolutionInfoDto(
                    IsResolved: false,
                    ResolutionCode: "appealed",
                    ResolutionText: "Апелляция отправлена. Жалоба повторно ожидает рассмотрения.",
                    AppealText: report.AppealText,
                    AppealedAt: report.AppealedAt
                ),

            ReportStatus.Pending
                => new ReportResolutionInfoDto(
                    IsResolved: false,
                    ResolutionCode: null,
                    ResolutionText: "Жалоба отправлена и ожидает проверки модератором.",
                    AppealText: report.AppealText,
                    AppealedAt: report.AppealedAt
                ),

            ReportStatus.Reviewed
                => new ReportResolutionInfoDto(
                    IsResolved: false,
                    ResolutionCode: "reviewed",
                    ResolutionText: "Жалоба просмотрена модератором и находится в работе.",
                    AppealText: report.AppealText,
                    AppealedAt: report.AppealedAt
                ),

            ReportStatus.Rejected
                => new ReportResolutionInfoDto(
                    IsResolved: true,
                    ResolutionCode: "rejected",
                    ResolutionText: "Жалоба была проверена и отклонена.",
                    AppealText: report.AppealText,
                    AppealedAt: report.AppealedAt
                ),

            ReportStatus.Resolved
                => BuildResolvedResolution(report),

            _ => new ReportResolutionInfoDto(
                IsResolved: false,
                ResolutionCode: null,
                ResolutionText: "Статус жалобы обновлён.",
                AppealText: report.AppealText,
                AppealedAt: report.AppealedAt
            )
        };
    }

    private static ReportResolutionInfoDto BuildResolvedResolution(Report report)
    {
        var code = report.DecisionAction switch
        {
            ReportDecisionAction.DeletePhoto => "resolved_delete_photo",
            ReportDecisionAction.DeleteComment => "resolved_delete_comment",
            ReportDecisionAction.DeactivateUser => "resolved_deactivate_user",
            ReportDecisionAction.None or null => "resolved_none",
            _ => "resolved"
        };

        var text = report.DecisionAction switch
        {
            ReportDecisionAction.DeletePhoto => "Жалоба подтверждена: публикация удалена.",
            ReportDecisionAction.DeleteComment => "Жалоба подтверждена: комментарий удалён.",
            ReportDecisionAction.DeactivateUser => "Жалоба подтверждена: пользователь деактивирован.",
            ReportDecisionAction.None or null => "Жалоба подтверждена и закрыта без дополнительных санкций.",
            _ => "Жалоба подтверждена и обработана."
        };

        return new ReportResolutionInfoDto(
            IsResolved: true,
            ResolutionCode: code,
            ResolutionText: text,
            AppealText: report.AppealText,
            AppealedAt: report.AppealedAt
        );
    }

    private static string BuildDisplayName(User user)
    {
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? user.UserName : fullName;
    }
}