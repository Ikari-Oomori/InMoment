namespace InMoment.Application.Features.SystemAnnouncements.List;

public sealed record SystemAnnouncementDto(
    Guid Id,
    string Text,
    string? MediaUrl,
    string? MediaContentType,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    bool CanEdit
);