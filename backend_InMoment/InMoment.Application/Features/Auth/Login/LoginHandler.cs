using FluentValidation;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using InMoment.Domain.Security;
using MediatR;

namespace InMoment.Application.Features.Auth.Login;

public sealed class LoginHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly IUserRepository _users;
    private readonly IRefreshSessionRepository _sessions;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly IGeoIpResolver _geoIpResolver;
    private readonly IUnitOfWork _uow;
    private readonly IValidator<LoginCommand> _validator;

    public LoginHandler(
        IUserRepository users,
        IRefreshSessionRepository sessions,
        IPasswordHasher hasher,
        ITokenService tokens,
        IRefreshTokenService refreshTokens,
        IGeoIpResolver geoIpResolver,
        IUnitOfWork uow,
        IValidator<LoginCommand> validator)
    {
        _users = users;
        _sessions = sessions;
        _hasher = hasher;
        _tokens = tokens;
        _refreshTokens = refreshTokens;
        _geoIpResolver = geoIpResolver;
        _uow = uow;
        _validator = validator;
    }

    public async Task<LoginResult> Handle(LoginCommand cmd, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(cmd, ct);

        var login = cmd.Email.Trim();
        var isEmailLogin = login.Contains('@');

        var normalizedEmail = login.ToLowerInvariant();
        var normalizedUserName = login;

        var deletedUser = isEmailLogin
            ? await _users.GetByDeletedEmailAsync(normalizedEmail, ct)
            : await _users.GetByDeletedUserNameAsync(normalizedUserName, ct);

        if (deletedUser is not null)
            throw new ForbiddenException("Аккаунт с таким email не найден.");

        var user = isEmailLogin
            ? await _users.GetByEmailAsync(normalizedEmail, ct)
            : await _users.GetByUserNameAsync(normalizedUserName, ct);

        if (user is null)
            throw new ForbiddenException(
                isEmailLogin
                    ? "Аккаунт с таким email не найден."
                    : "Аккаунт с таким username не найден.");

        if (!_hasher.Verify(cmd.Password, user.PasswordHash))
            throw new ForbiddenException("Неверный пароль.");

        if (!user.IsActive)
        {
            user.Reactivate();
        }

        var accessToken = _tokens.CreateAccessToken(user.Id, user.UserName);

        var rawRefreshToken = _refreshTokens.CreateToken();
        var refreshTokenHash = _refreshTokens.HashToken(rawRefreshToken);
        var refreshTokenExpiresAtUtc = _refreshTokens.GetExpiryUtc();

        var geo = await _geoIpResolver.ResolveAsync(cmd.IpAddress, ct);
        var nowUtc = DateTime.UtcNow;

        await RevokeMatchingActiveSessionsAsync(
            user.Id,
            cmd.DeviceName,
            cmd.Platform,
            cmd.UserAgent,
            nowUtc,
            ct);

        var session = RefreshSession.Create(
            userId: user.Id,
            tokenHash: refreshTokenHash,
            createdAtUtc: nowUtc,
            expiresAtUtc: refreshTokenExpiresAtUtc,
            deviceName: cmd.DeviceName,
            platform: cmd.Platform,
            ipAddress: cmd.IpAddress,
            userAgent: cmd.UserAgent,
            geoCountry: geo?.Country,
            geoRegion: geo?.Region,
            geoCity: geo?.City,
            geoProvider: geo?.Provider);

        await _sessions.AddAsync(session, ct);
        await _uow.SaveChangesAsync(ct);

        return new LoginResult(
            user.Id,
            accessToken,
            rawRefreshToken,
            refreshTokenExpiresAtUtc);
    }

    private async Task RevokeMatchingActiveSessionsAsync(
        Guid userId,
        string? deviceName,
        string? platform,
        string? userAgent,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var currentDevice = Normalize(deviceName);
        var currentPlatform = Normalize(platform);
        var currentAgent = NormalizeUserAgent(userAgent);

        var activeSessions = await _sessions.GetByUserIdAsync(userId, ct);

        foreach (var existing in activeSessions)
        {
            if (existing.IsRevoked)
                continue;

            var sameDevice =
                Normalize(existing.DeviceName) == currentDevice &&
                Normalize(existing.Platform) == currentPlatform &&
                NormalizeUserAgent(existing.UserAgent) == currentAgent;

            if (!sameDevice)
                continue;

            existing.Revoke("replaced_by_new_login_same_device", nowUtc);
        }
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim().ToLowerInvariant();
    }

    private static string? NormalizeUserAgent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length > 500)
            normalized = normalized[..500];

        return normalized;
    }
}