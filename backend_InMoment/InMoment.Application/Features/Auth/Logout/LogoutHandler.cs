using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using MediatR;

namespace InMoment.Application.Features.Auth.Logout;

public sealed class LogoutHandler : IRequestHandler<LogoutCommand>
{
    private readonly IRefreshSessionRepository _sessions;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly IUnitOfWork _uow;

    public LogoutHandler(
        IRefreshSessionRepository sessions,
        IRefreshTokenService refreshTokens,
        IUnitOfWork uow)
    {
        _sessions = sessions;
        _refreshTokens = refreshTokens;
        _uow = uow;
    }

    public async Task Handle(LogoutCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.RefreshToken))
            return;

        var hash = _refreshTokens.HashToken(cmd.RefreshToken);
        var session = await _sessions.GetByTokenHashAsync(hash, ct);

        if (session is null)
            return;

        session.Revoke("logout", DateTime.UtcNow);
        await _uow.SaveChangesAsync(ct);
    }
}