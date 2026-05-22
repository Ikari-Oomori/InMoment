using DomainValidationException = InMoment.Domain.Common.ValidationException;
using FluentValidation;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Security;
using InMoment.Domain.Users;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Auth.Register;

public sealed class RegisterHandler : IRequestHandler<RegisterCommand, RegisterResult>
{
    private readonly IUserRepository _users;
    private readonly IRefreshSessionRepository _sessions;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly IUnitOfWork _uow;
    private readonly IValidator<RegisterCommand> _validator;

    public RegisterHandler(
        IUserRepository users,
        IRefreshSessionRepository sessions,
        IPasswordHasher hasher,
        ITokenService tokens,
        IRefreshTokenService refreshTokens,
        IUnitOfWork uow,
        IValidator<RegisterCommand> validator)
    {
        _users = users;
        _sessions = sessions;
        _hasher = hasher;
        _tokens = tokens;
        _refreshTokens = refreshTokens;
        _uow = uow;
        _validator = validator;
    }

    public async Task<RegisterResult> Handle(RegisterCommand cmd, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(cmd, ct);

        var email = cmd.Email.Trim().ToLowerInvariant();
        var userName = cmd.UserName.Trim();
        var phoneNumber = PhoneNumberNormalizer.Normalize(cmd.PhoneNumber);

        if (await _users.EmailExistsAsync(email, ct))
            throw new DomainValidationException("Email is already used.");

        if (await _users.UserNameExistsAsync(userName, ct))
            throw new DomainValidationException("Nickname is already used.");

        if (phoneNumber is not null && await _users.PhoneNumberExistsAsync(phoneNumber, ct))
            throw new DomainValidationException("Phone number is already used.");

        var hash = _hasher.Hash(cmd.Password);

        var user = User.Create(
            email: email,
            userName: userName,
            firstName: cmd.FirstName,
            lastName: cmd.LastName,
            passwordHash: hash,
            phoneNumber: phoneNumber);

        await _users.AddAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        var accessToken = _tokens.CreateAccessToken(user.Id, user.UserName);

        var rawRefreshToken = _refreshTokens.CreateToken();
        var refreshTokenHash = _refreshTokens.HashToken(rawRefreshToken);
        var refreshTokenExpiresAtUtc = _refreshTokens.GetExpiryUtc();

        var session = RefreshSession.Create(
             userId: user.Id,
             tokenHash: refreshTokenHash,
             createdAtUtc: DateTime.UtcNow,
             expiresAtUtc: refreshTokenExpiresAtUtc,
             deviceName: "register",
             platform: null,
             ipAddress: null,
             userAgent: null,
             geoCountry: null,
             geoRegion: null,
             geoCity: null,
             geoProvider: null);

        await _sessions.AddAsync(session, ct);
        await _uow.SaveChangesAsync(ct);

        return new RegisterResult(
            user.Id,
            accessToken,
            rawRefreshToken,
            refreshTokenExpiresAtUtc);
    }

}