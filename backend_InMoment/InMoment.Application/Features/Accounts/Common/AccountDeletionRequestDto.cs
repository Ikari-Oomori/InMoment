namespace InMoment.Application.Features.Accounts.Common;

public sealed record AccountDeletionRequestDto(
    Guid Id,
    Guid UserId,
    int StatusCode,
    string Status,
    string RequestedEmail,
    string RequestedUserName,
    string? Note,
    string? ProcessingNote,
    Guid? ProcessedByUserId,
    DateTime RequestedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? ProcessedAtUtc
);