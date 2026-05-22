using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Sessions.List;

public sealed class ListSessionsHandler : IRequestHandler<ListSessionsQuery, IReadOnlyList<SessionDto>>
{
    private readonly IRefreshSessionRepository _sessions;
    private readonly ICurrentUser _current;
    private readonly IRefreshTokenService _refreshTokens;

    public ListSessionsHandler(
        IRefreshSessionRepository sessions,
        ICurrentUser current,
        IRefreshTokenService refreshTokens)
    {
        _sessions = sessions;
        _current = current;
        _refreshTokens = refreshTokens;
    }

    public async Task<IReadOnlyList<SessionDto>> Handle(ListSessionsQuery query, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Unauthorized.");

        string? currentHash = null;
        if (!string.IsNullOrWhiteSpace(query.CurrentRefreshToken))
            currentHash = _refreshTokens.HashToken(query.CurrentRefreshToken);

        var sessions = await _sessions.GetByUserIdAsync(_current.UserId, ct);

        return sessions
            .OrderByDescending(x => currentHash is not null && x.TokenHash == currentHash)
            .ThenByDescending(x => x.LastUsedAtUtc ?? x.CreatedAtUtc)
            .Select(x => new SessionDto(
                x.Id,
                x.DeviceName,
                x.Platform,
                x.IpAddress,
                x.UserAgent,
                x.GeoCountry,
                x.GeoRegion,
                x.GeoCity,
                x.CreatedAtUtc,
                x.LastUsedAtUtc,
                x.ExpiresAtUtc,
                currentHash is not null && x.TokenHash == currentHash,
                x.IsRevoked))
            .ToList();
    }
}