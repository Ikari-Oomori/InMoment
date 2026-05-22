using InMoment.Domain.Common;

namespace InMoment.Domain.Reports;

public sealed class Report : Entity<Guid>
{
    public Guid ReporterUserId { get; private set; }

    public ReportTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }

    public ReportReason Reason { get; private set; }
    public string? Description { get; private set; }

    public ReportStatus Status { get; private set; }
    public ReportDecisionAction? DecisionAction { get; private set; }

    public Guid? ReviewedByUserId { get; private set; }
    public DateTime? ReviewedAt { get; private set; }

    public string? AppealText { get; private set; }
    public DateTime? AppealedAt { get; private set; }
    public Guid? AppealedByUserId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private Report() { }

    public static Report Create(
        Guid reporterUserId,
        ReportTargetType targetType,
        Guid targetId,
        ReportReason reason,
        string? description)
    {
        if (reporterUserId == Guid.Empty)
            throw new ValidationException("ReporterUserId is required.");

        if (targetId == Guid.Empty)
            throw new ValidationException("TargetId is required.");

        var normalizedDescription = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();

        if (normalizedDescription is { Length: > 500 })
            throw new ValidationException("Description must be 500 characters or less.");

        return new Report
        {
            Id = Guid.NewGuid(),
            ReporterUserId = reporterUserId,
            TargetType = targetType,
            TargetId = targetId,
            Reason = reason,
            Description = normalizedDescription,
            Status = ReportStatus.Pending,
            DecisionAction = null,
            ReviewedByUserId = null,
            ReviewedAt = null,
            AppealText = null,
            AppealedAt = null,
            AppealedByUserId = null,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkReviewed(
        Guid reviewerUserId,
        ReportStatus status,
        ReportDecisionAction? decisionAction = null)
    {
        if (reviewerUserId == Guid.Empty)
            throw new ValidationException("ReviewerUserId is required.");

        if (status == ReportStatus.Pending)
            throw new ValidationException("Pending is not a valid review result.");

        if (Status != ReportStatus.Pending)
            throw new ValidationException("Report has already been reviewed.");

        if (status != ReportStatus.Resolved &&
            decisionAction.HasValue &&
            decisionAction.Value != ReportDecisionAction.None)
        {
            throw new ValidationException("DecisionAction is valid only for resolved reports.");
        }

        Status = status;
        ReviewedByUserId = reviewerUserId;
        ReviewedAt = DateTime.UtcNow;

        DecisionAction = status == ReportStatus.Resolved
            ? decisionAction ?? ReportDecisionAction.None
            : null;
    }

    public void SubmitAppeal(Guid userId, string text)
    {
        if (userId == Guid.Empty)
            throw new ValidationException("UserId is required.");

        if (ReporterUserId != userId)
            throw new ValidationException("Only reporter can submit appeal.");

        if (Status == ReportStatus.Pending)
            throw new ValidationException("Нельзя подать апелляцию до принятия решения.");

        if (!string.IsNullOrWhiteSpace(AppealText))
            throw new ValidationException("Апелляция уже была отправлена.");

        var normalized = string.IsNullOrWhiteSpace(text)
            ? null
            : text.Trim();

        if (string.IsNullOrWhiteSpace(normalized))
            throw new ValidationException("Текст апелляции обязателен.");

        if (normalized.Length > 1000)
            throw new ValidationException("Текст апелляции не должен превышать 1000 символов.");

        AppealText = normalized;
        AppealedAt = DateTime.UtcNow;
        AppealedByUserId = userId;

        Status = ReportStatus.Pending;
        DecisionAction = null;
        ReviewedByUserId = null;
        ReviewedAt = null;
    }
}