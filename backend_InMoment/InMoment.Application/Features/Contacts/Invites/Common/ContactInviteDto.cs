using InMoment.Domain.Contacts;

namespace InMoment.Application.Features.Contacts.Invites.Common;

public sealed record ContactInviteDto(
    Guid Id,
    ContactInviteChannel Channel,
    string? Email,
    string? PhoneNumber,
    string? DisplayName,
    string InviteToken,
    ContactInviteStatus Status,
    DateTime CreatedAtUtc,
    DateTime? CancelledAtUtc
);