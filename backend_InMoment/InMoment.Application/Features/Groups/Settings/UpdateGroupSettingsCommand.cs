using MediatR;

namespace InMoment.Application.Features.Groups.Settings;

public sealed record UpdateGroupSettingsCommand(
    Guid GroupId,
    string Name,
    string? Description
) : IRequest<GroupSettingsDto>;