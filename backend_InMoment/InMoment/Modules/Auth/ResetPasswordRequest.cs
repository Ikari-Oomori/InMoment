namespace InMoment.API.Modules.Auth;

public sealed record ResetPasswordRequest(
    string Token,
    string NewPassword
);