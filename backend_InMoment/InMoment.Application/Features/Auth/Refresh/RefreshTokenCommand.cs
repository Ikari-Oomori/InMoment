using MediatR;

namespace InMoment.Application.Features.Auth.Refresh;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<RefreshTokenResult>;