using MediatR;

namespace InMoment.Application.Features.Auth.Login;

public sealed record LoginCommand(
    string Email,
    string Password,
    string? DeviceName,
    string? Platform,
    string? IpAddress,
    string? UserAgent
) : IRequest<LoginResult>;