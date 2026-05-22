namespace InMoment.Application.Features.Auth.Refresh;

public sealed record RefreshTokenResult(
    string AccessToken,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc
);