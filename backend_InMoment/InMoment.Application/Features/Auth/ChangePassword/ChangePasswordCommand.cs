using MediatR;

namespace InMoment.Application.Features.Auth.ChangePassword;

public sealed record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword,
    string? CurrentRefreshToken
) : IRequest;