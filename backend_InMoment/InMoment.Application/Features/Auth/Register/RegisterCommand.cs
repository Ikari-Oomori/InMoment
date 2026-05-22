using MediatR;

namespace InMoment.Application.Features.Auth.Register;

public sealed record RegisterCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string UserName,
    string? PhoneNumber = null
) : IRequest<RegisterResult>;