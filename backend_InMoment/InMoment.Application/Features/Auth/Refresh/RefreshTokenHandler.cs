using FluentValidation;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Auth.Refresh;

public sealed class RefreshTokenHandler : IRequestHandler<RefreshTokenCommand, RefreshTokenResult>
{
    private readonly IRefreshSessionRepository _sessions;
    private readonly IUserRepository _users;
    private readonly ITokenService _tokens;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly IUnitOfWork _uow;
    private readonly IValidator<RefreshTokenCommand> _validator;

    public RefreshTokenHandler(
        IRefreshSessionRepository sessions,
        IUserRepository users,
        ITokenService tokens,
        IRefreshTokenService refreshTokens,
        IUnitOfWork uow,
        IValidator<RefreshTokenCommand> validator)
    {
        _sessions = sessions;
        _users = users;
        _tokens = tokens;
        _refreshTokens = refreshTokens;
        _uow = uow;
        _validator = validator;
    }

    public async Task<RefreshTokenResult> Handle(RefreshTokenCommand cmd, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(cmd, ct);

        var now = DateTime.UtcNow;

        var hash = _refreshTokens.HashToken(cmd.RefreshToken);
        var session = await _sessions.GetByTokenHashAsync(hash, ct)
                     ?? throw new ForbiddenException("Invalid refresh token.");

        if (!session.IsActive(now))
            throw new ForbiddenException("Refresh session expired or revoked.");

        var user = await _users.GetByIdAsync(session.UserId, ct)
                   ?? throw new NotFoundException("User not found.");

        if (!user.IsActive)
        {
            session.Revoke("account_deactivated", now);
            await _uow.SaveChangesAsync(ct);
            throw new ForbiddenException("Account is deactivated.");
        }

        var newRawRefreshToken = _refreshTokens.CreateToken();
        var newHash = _refreshTokens.HashToken(newRawRefreshToken);
        var newExpiry = _refreshTokens.GetExpiryUtc();

        session.Rotate(newHash, newExpiry, now);

        var accessToken = _tokens.CreateAccessToken(user.Id, user.UserName);

        await _uow.SaveChangesAsync(ct);

        return new RefreshTokenResult(
            accessToken,
            newRawRefreshToken,
            newExpiry);
    }
}