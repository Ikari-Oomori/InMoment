namespace InMoment.Application.Features.Contacts.Common;

public sealed record ImportContactsResultDto(
    IReadOnlyList<ContactMatchDto> Matches,
    IReadOnlyList<ContactInviteCandidateDto> Invites
);