using InMoment.Application.Features.Contacts.Invites.Common;
using MediatR;

namespace InMoment.Application.Features.Contacts.Invites.Send;

public sealed record SendContactInviteCommand(
    string? Email,
    string? PhoneNumber,
    string? DisplayName) : IRequest<ContactInviteDto>;