using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Users.SetActiveGroup;

public sealed class SetActiveGroupHandler : IRequestHandler<SetActiveGroupCommand, Unit>
{
    private readonly IUserRepository _users;
    private readonly IGroupRepository _groups;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _current;

    public SetActiveGroupHandler(
        IUserRepository users,
        IGroupRepository groups,
        IUnitOfWork uow,
        ICurrentUser current)
    {
        _users = users;
        _groups = groups;
        _uow = uow;
        _current = current;
    }

    public async Task<Unit> Handle(SetActiveGroupCommand cmd, CancellationToken ct)
    {
        if (cmd.GroupId == Guid.Empty)
            throw new ValidationException("GroupId is required.");

        var user = await _users.GetByIdAsync(_current.UserId, ct)
                   ?? throw new NotFoundException("User not found.");

        var group = await _groups.GetByIdAsync(cmd.GroupId, ct)
                    ?? throw new NotFoundException("Group not found.");

        group.EnsureMember(_current.UserId);

        user.SetActiveGroup(group.Id);

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}