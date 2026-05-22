using MediatR;

namespace InMoment.Application.Features.Sessions.List;

public sealed record ListSessionsQuery(string? CurrentRefreshToken) : IRequest<IReadOnlyList<SessionDto>>;