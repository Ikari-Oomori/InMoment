namespace InMoment.API.Modules.Auth;
public sealed record LoginRequest(
    string Email,
    string Password,
    string? DeviceName,
    string? Platform
);