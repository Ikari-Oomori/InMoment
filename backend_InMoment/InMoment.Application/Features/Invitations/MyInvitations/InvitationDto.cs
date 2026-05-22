namespace InMoment.Application.Features.Invitations.MyInvitations;

public sealed record InvitationDto(
    Guid Id,
    Guid GroupId,
    Guid InvitedByUserId,
    DateTime CreatedAt
);