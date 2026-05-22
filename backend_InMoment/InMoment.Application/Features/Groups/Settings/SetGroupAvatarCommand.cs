using MediatR;

namespace InMoment.Application.Features.Groups.Settings;

public sealed record SetGroupAvatarCommand(
    Guid GroupId,
    string? AvatarUrl
) : IRequest<Unit>;