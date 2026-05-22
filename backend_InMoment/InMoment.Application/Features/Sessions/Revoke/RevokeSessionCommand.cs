using MediatR;

namespace InMoment.Application.Features.Sessions.Revoke;

public sealed record RevokeSessionCommand(Guid SessionId) : IRequest;