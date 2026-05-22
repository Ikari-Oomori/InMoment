using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Groups.Settings;

public sealed class UpdateGroupSettingsHandler : IRequestHandler<UpdateGroupSettingsCommand, GroupSettingsDto>
{
    private readonly IGroupRepository _groups;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _current;

    public UpdateGroupSettingsHandler(
        IGroupRepository groups,
        IUnitOfWork uow,
        ICurrentUser current)
    {
        _groups = groups;
        _uow = uow;
        _current = current;
    }

    public async Task<GroupSettingsDto> Handle(UpdateGroupSettingsCommand cmd, CancellationToken ct)
    {
        var group = await _groups.GetByIdAsync(cmd.GroupId, ct)
                   ?? throw new NotFoundException("Group not found.");

        group.UpdateSettings(_current.UserId, cmd.Name, cmd.Description);

        await _uow.SaveChangesAsync(ct);

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