using DomainValidationException = InMoment.Domain.Common.ValidationException;
using FluentValidation;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Auth.ChangePassword;

public sealed class ChangePasswordHandler : IRequestHandler<ChangePasswordCommand>
{
    private readonly IUserRepository _users;
    private readonly IRefreshSessionRepository _sessions;
    private readonly ICurrentUser _current;
    private readonly IPasswordHasher _hasher;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly IUnitOfWork _uow;
    private readonly IValidator<ChangePasswordCommand> _validator;

    public ChangePasswordHandler(
        IUserRepository users,
        IRefreshSessionRepository sessions,
        ICurrentUser current,
        IPasswordHasher hasher,
        IRefreshTokenService refreshTokens,
        IUnitOfWork uow,
        IValidator<ChangePasswordCommand> validator)
    {
        _users = users;
        _sessions = sessions;
        _current = current;
        _hasher = hasher;
        _refreshTokens = refreshTokens;
        _uow = uow;
        _validator = validator;
    }

    public async Task Handle(ChangePasswordCommand cmd, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(cmd, ct);

        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Unauthorized.");

        var user = await _users.GetByIdAsync(_current.UserId, ct)
                   ?? throw new NotFoundException("User not found.");

        user.EnsureActive();

        if (!_hasher.Verify(cmd.CurrentPassword, user.PasswordHash))
            throw new DomainValidationException("Current password is incorrect.");

        if (_hasher.Verify(cmd.NewPassword, user.PasswordHash))
            throw new DomainValidationException("New password must be different from the current password.");

        user.ChangePasswordHash(_hasher.Hash(cmd.NewPassword));

        var now = DateTime.UtcNow;
        string? currentSessionHash = null;

        if (!string.IsNullOrWhiteSpace(cmd.CurrentRefreshToken))
            currentSessionHash = _refreshTokens.HashToken(cmd.CurrentRefreshToken);

        var sessions = await _sessions.GetByUserIdAsync(user.Id, ct);

        foreach (var session in sessions.Where(x => x.IsActive(now)))
        {
            if (currentSessionHash is not null && session.TokenHash == currentSessionHash)
                continue;

            session.Revoke("password_change", now);
        }

        await _uow.SaveChangesAsync(ct);
    }
}