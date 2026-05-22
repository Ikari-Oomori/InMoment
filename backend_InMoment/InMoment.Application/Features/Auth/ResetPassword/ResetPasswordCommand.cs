using MediatR;

namespace InMoment.Application.Features.Auth.ResetPassword;

public sealed record ResetPasswordCommand(
    string Token,
    string NewPassword
) : IRequest;