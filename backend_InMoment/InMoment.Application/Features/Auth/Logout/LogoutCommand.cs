using MediatR;

namespace InMoment.Application.Features.Auth.Logout;

public sealed record LogoutCommand(string RefreshToken) : IRequest;