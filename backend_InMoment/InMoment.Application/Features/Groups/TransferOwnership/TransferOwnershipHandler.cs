using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Groups.TransferOwnership;

public sealed class TransferOwnershipHandler
    : IRequestHandler<TransferOwnershipCommand, Unit>
{
    private readonly IGroupRepository _groups;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _current;

    public TransferOwnershipHandler(
        IGroupRepository groups,
        IUnitOfWork uow,
        ICurrentUser current)
    {
        _groups = groups;
        _uow = uow;
        _current = current;
    }

    public async Task<Unit> Handle(TransferOwnershipCommand cmd, CancellationToken ct)
    {
        var group = await _groups.GetByIdAsync(cmd.GroupId, ct)
                   ?? throw new NotFoundException("Group not found.");

        await using var tx = await _uow.BeginTransactionAsync(ct);

        group.DemoteOwnerToAdmin(_current.UserId, cmd.NewOwnerUserId);
        await _uow.SaveChangesAsync(ct);

        group.PromoteAdminOrMemberToOwner(_current.UserId, cmd.NewOwnerUserId);
        await _uow.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
        return Unit.Value;
    }
}