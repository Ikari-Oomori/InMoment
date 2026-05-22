namespace InMoment.Application.Features.Auth.Register;

public sealed record RegisterResult(
    Guid UserId,
    string AccessToken,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc
);