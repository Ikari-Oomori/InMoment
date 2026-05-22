using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Sessions.RevokeOthers;

public sealed class RevokeOtherSessionsHandler : IRequestHandler<RevokeOtherSessionsCommand, int>
{
    private readonly IRefreshSessionRepository _sessions;
    private readonly ICurrentUser _current;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly IUnitOfWork _uow;

    public RevokeOtherSessionsHandler(
        IRefreshSessionRepository sessions,
        ICurrentUser current,
        IRefreshTokenService refreshTokens,
        IUnitOfWork uow)
    {
        _sessions = sessions;
        _current = current;
        _refreshTokens = refreshTokens;
        _uow = uow;
    }

    public async Task<int> Handle(RevokeOtherSessionsCommand request, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Unauthorized.");

        string? currentHash = null;
        if (!string.IsNullOrWhiteSpace(request.CurrentRefreshToken))
            currentHash = _refreshTokens.HashToken(request.CurrentRefreshToken);

        var sessions = await _sessions.GetByUserIdAsync(_current.UserId, ct);
        var nowUtc = DateTime.UtcNow;
        var revokedCount = 0;

        foreach (var session in sessions)
        {
            if (session.IsRevoked)
                continue;

            if (currentHash is not null && session.TokenHash == currentHash)
                continue;

            session.Revoke("manual_revoke_others", nowUtc);
            revokedCount++;
        }

        await _uow.SaveChangesAsync(ct);
        return revokedCount;
    }
}