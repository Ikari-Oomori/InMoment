using MediatR;

namespace InMoment.Application.Features.Contacts.Invites.Cancel;

public sealed record CancelContactInviteCommand(Guid InviteId) : IRequest;