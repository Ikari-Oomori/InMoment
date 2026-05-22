using FluentValidation;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Auth.ResetPassword;

public sealed class ResetPasswordHandler : IRequestHandler<ResetPasswordCommand>
{
    private readonly IUserRepository _users;
    private readonly IPasswordResetTokenRepository _resetTokens;
    private readonly IRefreshSessionRepository _sessions;
    private readonly IPasswordHasher _hasher;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly IUnitOfWork _uow;
    private readonly IValidator<ResetPasswordCommand> _validator;

    public ResetPasswordHandler(
        IUserRepository users,
        IPasswordResetTokenRepository resetTokens,
        IRefreshSessionRepository sessions,
        IPasswordHasher hasher,
        IRefreshTokenService refreshTokens,
        IUnitOfWork uow,
        IValidator<ResetPasswordCommand> validator)
    {
        _users = users;
        _resetTokens = resetTokens;
        _sessions = sessions;
        _hasher = hasher;
        _refreshTokens = refreshTokens;
        _uow = uow;
        _validator = validator;
    }

    public async Task Handle(ResetPasswordCommand cmd, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(cmd, ct);

        var now = DateTime.UtcNow;

        var hash = _refreshTokens.HashToken(cmd.Token);
        var resetToken = await _resetTokens.GetByTokenHashAsync(hash, ct)
                         ?? throw new ForbiddenException("Invalid reset token.");

        if (!resetToken.IsActive(now))
            throw new ForbiddenException("Reset token expired or invalid.");

        var user = await _users.GetByIdAsync(resetToken.UserId, ct)
                   ?? throw new NotFoundException("User not found.");

        if (!user.IsActive)
        {
            resetToken.Revoke(now);
            await _uow.SaveChangesAsync(ct);
            throw new ForbiddenException("Account is deactivated.");
        }

        user.ChangePasswordHash(_hasher.Hash(cmd.NewPassword));
        resetToken.MarkUsed(now);

        var sessions = await _sessions.GetByUserIdAsync(user.Id, ct);

        foreach (var session in sessions.Where(x => x.RevokedAtUtc == null))
            session.Revoke("password_reset", now);

        await _uow.SaveChangesAsync(ct);
    }
}