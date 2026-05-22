namespace InMoment.API.Modules.Auth;

public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string? CurrentRefreshToken
);