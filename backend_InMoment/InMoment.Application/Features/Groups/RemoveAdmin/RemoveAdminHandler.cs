using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Groups.RemoveAdmin;

public sealed class RemoveAdminHandler : IRequestHandler<RemoveAdminCommand, Unit>
{
    private readonly IGroupRepository _groups;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _current;

    public RemoveAdminHandler(
        IGroupRepository groups,
        IUnitOfWork uow,
        ICurrentUser current)
    {
        _groups = groups;
        _uow = uow;
        _current = current;
    }

    public async Task<Unit> Handle(RemoveAdminCommand cmd, CancellationToken ct)
    {
        var group = await _groups.GetByIdAsync(cmd.GroupId, ct)
                   ?? throw new NotFoundException("Group not found.");

        group.DemoteAdmin(_current.UserId, cmd.UserId);

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}