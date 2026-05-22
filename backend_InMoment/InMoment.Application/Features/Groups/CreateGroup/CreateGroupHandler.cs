using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using MediatR;

namespace InMoment.Application.Features.Groups.CreateGroup;

public sealed class CreateGroupHandler : IRequestHandler<CreateGroupCommand, CreateGroupResult>
{
    private readonly IGroupRepository _groups;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public CreateGroupHandler(IGroupRepository groups, IUnitOfWork uow, ICurrentUser currentUser)
    {
        _groups = groups;
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<CreateGroupResult> Handle(CreateGroupCommand cmd, CancellationToken ct)
    {
        var group = Domain.Groups.Group.Create(cmd.Name, _currentUser.UserId);
        await _groups.AddAsync(group, ct);
        await _uow.SaveChangesAsync(ct);
        return new CreateGroupResult(group.Id);
    }
}