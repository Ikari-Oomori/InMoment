using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Auth.LogoutAll;

public sealed class LogoutAllHandler : IRequestHandler<LogoutAllCommand>
{
    private readonly IRefreshSessionRepository _sessions;
    private readonly ICurrentUser _current;
    private readonly IUnitOfWork _uow;

    public LogoutAllHandler(
        IRefreshSessionRepository sessions,
        ICurrentUser current,
        IUnitOfWork uow)
    {
        _sessions = sessions;
        _current = current;
        _uow = uow;
    }

    public async Task Handle(LogoutAllCommand request, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Unauthorized.");

        var sessions = await _sessions.GetByUserIdAsync(_current.UserId, ct);

        foreach (var session in sessions.Where(x => x.IsActive(DateTime.UtcNow)))
            session.Revoke("logout_all", DateTime.UtcNow);

        await _uow.SaveChangesAsync(ct);
    }
}