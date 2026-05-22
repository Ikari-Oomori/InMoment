using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Groups.Settings;

public sealed class SetGroupAvatarHandler : IRequestHandler<SetGroupAvatarCommand, Unit>
{
    private readonly IGroupRepository _groups;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _current;

    public SetGroupAvatarHandler(
        IGroupRepository groups,
        IUnitOfWork uow,
        ICurrentUser current)
    {
        _groups = groups;
        _uow = uow;
        _current = current;
    }

    public async Task<Unit> Handle(SetGroupAvatarCommand cmd, CancellationToken ct)
    {
        var group = await _groups.GetByIdAsync(cmd.GroupId, ct)
                   ?? throw new NotFoundException("Group not found.");

        group.SetAvatar(_current.UserId, cmd.AvatarUrl);

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}