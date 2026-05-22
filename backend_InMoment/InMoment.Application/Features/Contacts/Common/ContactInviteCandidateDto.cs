namespace InMoment.Application.Features.Contacts.Common;

public sealed record ContactInviteCandidateDto(
    string? DisplayName,
    string? Phone,
    string? Email
);