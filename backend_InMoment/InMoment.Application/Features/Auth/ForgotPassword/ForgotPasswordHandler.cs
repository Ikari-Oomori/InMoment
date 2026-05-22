using FluentValidation;
using InMoment.Application.Abstractions.Communication;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Security;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InMoment.Application.Features.Auth.ForgotPassword;

public sealed class ForgotPasswordHandler : IRequestHandler<ForgotPasswordCommand>
{
    private readonly IUserRepository _users;
    private readonly IPasswordResetTokenRepository _tokens;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly IPasswordRecoverySender _sender;
    private readonly IUnitOfWork _uow;
    private readonly IValidator<ForgotPasswordCommand> _validator;
    private readonly ILogger<ForgotPasswordHandler> _logger;

    public ForgotPasswordHandler(
        IUserRepository users,
        IPasswordResetTokenRepository tokens,
        IRefreshTokenService refreshTokens,
        IPasswordRecoverySender sender,
        IUnitOfWork uow,
        IValidator<ForgotPasswordCommand> validator,
        ILogger<ForgotPasswordHandler> logger)
    {
        _users = users;
        _tokens = tokens;
        _refreshTokens = refreshTokens;
        _sender = sender;
        _uow = uow;
        _validator = validator;
        _logger = logger;
    }

    public async Task Handle(ForgotPasswordCommand cmd, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(cmd, ct);

        var email = cmd.Email.Trim().ToLowerInvariant();
        var user = await _users.GetByEmailAsync(email, ct);

        if (user is null || !user.IsActive)
            return;

        var activeTokens = await _tokens.GetActiveByUserIdAsync(user.Id, ct);
        foreach (var token in activeTokens)
            token.Revoke(DateTime.UtcNow);

        var rawToken = _refreshTokens.CreateToken();
        var tokenHash = _refreshTokens.HashToken(rawToken);

        var resetToken = PasswordResetToken.Create(
            userId: user.Id,
            tokenHash: tokenHash,
            createdAtUtc: DateTime.UtcNow,
            expiresAtUtc: DateTime.UtcNow.AddHours(1),
            requestedByIp: cmd.RequestedByIp,
            requestedByUserAgent: cmd.RequestedByUserAgent);

        await _tokens.AddAsync(resetToken, ct);
        await _uow.SaveChangesAsync(ct);

        try
        {
            await _sender.SendResetPasswordAsync(
                user.Email,
                $"{user.FirstName} {user.LastName}".Trim(),
                rawToken,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Password recovery email delivery failed for {Email}. Token created, but email was not delivered.",
                user.Email);
        }
    }
}