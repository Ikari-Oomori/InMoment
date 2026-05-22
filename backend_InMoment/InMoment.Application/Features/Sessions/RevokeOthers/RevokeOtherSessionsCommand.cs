using MediatR;

namespace InMoment.Application.Features.Sessions.RevokeOthers;

public sealed record RevokeOtherSessionsCommand(string? CurrentRefreshToken) : IRequest<int>;