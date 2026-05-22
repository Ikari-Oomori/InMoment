using MediatR;

namespace InMoment.Application.Features.Auth.ForgotPassword;

public sealed record ForgotPasswordCommand(
    string Email,
    string? RequestedByIp,
    string? RequestedByUserAgent
) : IRequest;