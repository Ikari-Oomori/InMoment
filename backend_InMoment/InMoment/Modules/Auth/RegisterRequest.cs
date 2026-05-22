namespace InMoment.API.Modules.Auth;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string UserName,
    string? PhoneNumber
);