using InMoment.Domain.Reports;

namespace InMoment.Application.Features.Reports.Common;

public static class ReportResolutionMapper
{
    public static ReportResolutionInfoDto Map(Report report)
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
                => new ReportResolutionInfoDto(
                    IsResolved: true,
                    ResolutionCode: "resolved",
                    ResolutionText: "Жалоба подтверждена и обработана.",
                    AppealText: report.AppealText,
                    AppealedAt: report.AppealedAt
                ),

            _ => new ReportResolutionInfoDto(
                IsResolved: false,
                ResolutionCode: null,
                ResolutionText: "Статус жалобы обновлён.",
                AppealText: report.AppealText,
                AppealedAt: report.AppealedAt
            )
        };
    }
}