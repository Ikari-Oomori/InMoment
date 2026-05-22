using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Groups.Settings;

public sealed class GetGroupSettingsHandler : IRequestHandler<GetGroupSettingsQuery, GroupSettingsDto>
{
    private readonly IGroupRepository _groups;
    private readonly ICurrentUser _current;

    public GetGroupSettingsHandler(IGroupRepository groups, ICurrentUser current)
    {
        _groups = groups;
        _current = current;
    }

    public async Task<GroupSettingsDto> Handle(GetGroupSettingsQuery q, CancellationToken ct)
    {
        var group = await _groups.GetByIdAsync(q.GroupId, ct)
                   ?? throw new NotFoundException("Group not found.");

        group.EnsureMember(_current.UserId);

        return new GroupSettingsDto(
            group.Id,
            group.Name,
            group.Description,
            group.AvatarUrl,
            group.OwnerId,
            group.CreatedAt
        );
    }
}