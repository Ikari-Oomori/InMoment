namespace InMoment.Application.Features.Auth.Login;

public sealed record LoginResult(
    Guid UserId,
    string AccessToken,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc
);