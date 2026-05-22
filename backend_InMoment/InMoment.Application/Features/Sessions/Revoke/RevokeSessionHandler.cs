using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Sessions.Revoke;

public sealed class RevokeSessionHandler : IRequestHandler<RevokeSessionCommand>
{
    private readonly IRefreshSessionRepository _sessions;
    private readonly ICurrentUser _current;
    private readonly IUnitOfWork _uow;

    public RevokeSessionHandler(
        IRefreshSessionRepository sessions,
        ICurrentUser current,
        IUnitOfWork uow)
    {
        _sessions = sessions;
        _current = current;
        _uow = uow;
    }

    public async Task Handle(RevokeSessionCommand cmd, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Unauthorized.");

        var session = await _sessions.GetByIdAsync(cmd.SessionId, ct)
                     ?? throw new NotFoundException("Session not found.");

        if (session.UserId != _current.UserId)
            throw new ForbiddenException("You cannot revoke another user's session.");

        session.Revoke("manual_revoke", DateTime.UtcNow);
        await _uow.SaveChangesAsync(ct);
    }
}