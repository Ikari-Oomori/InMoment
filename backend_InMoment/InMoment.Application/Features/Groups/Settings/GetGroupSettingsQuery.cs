using MediatR;

namespace InMoment.Application.Features.Groups.Settings;

public sealed record GetGroupSettingsQuery(Guid GroupId) : IRequest<GroupSettingsDto>;

public sealed record GroupSettingsDto(
    Guid Id,
    string Name,
    string? Description,
    string? AvatarUrl,
    Guid OwnerId,
    DateTime CreatedAt
);