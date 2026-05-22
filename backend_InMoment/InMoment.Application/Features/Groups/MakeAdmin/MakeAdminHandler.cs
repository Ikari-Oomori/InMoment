using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Groups.MakeAdmin;

public sealed class MakeAdminHandler : IRequestHandler<MakeAdminCommand, Unit>
{
    private readonly IGroupRepository _groups;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _current;

    public MakeAdminHandler(
        IGroupRepository groups,
        IUnitOfWork uow,
        ICurrentUser current)
    {
        _groups = groups;
        _uow = uow;
        _current = current;
    }

    public async Task<Unit> Handle(MakeAdminCommand cmd, CancellationToken ct)
    {
        var group = await _groups.GetByIdAsync(cmd.GroupId, ct)
                   ?? throw new NotFoundException("Group not found.");

        group.PromoteToAdmin(_current.UserId, cmd.UserId);

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}