using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using InMoment.Domain.Privacy;
using MediatR;

namespace InMoment.Application.Features.Blocks.BlockUser;

public sealed class BlockUserHandler : IRequestHandler<BlockUserCommand>
{
    private readonly IBlockedUserRepository _blocks;
    private readonly IUserRepository _users;
    private readonly ICurrentUser _current;
    private readonly IUnitOfWork _uow;

    public BlockUserHandler(
        IBlockedUserRepository blocks,
        IUserRepository users,
        ICurrentUser current,
        IUnitOfWork uow)
    {
        _blocks = blocks;
        _users = users;
        _current = current;
        _uow = uow;
    }

    public async Task Handle(BlockUserCommand cmd, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        if (cmd.BlockedUserId == Guid.Empty)
            throw new ValidationException("BlockedUserId is required.");

        var targetUser = await _users.GetByIdAsync(cmd.BlockedUserId, ct)
                         ?? throw new NotFoundException("Пользователь не найден.");

        if (await _blocks.ExistsAsync(_current.UserId, targetUser.Id, ct))
            return;

        var block = BlockedUser.Create(_current.UserId, targetUser.Id);
        await _blocks.AddAsync(block, ct);
        await _uow.SaveChangesAsync(ct);
    }
}