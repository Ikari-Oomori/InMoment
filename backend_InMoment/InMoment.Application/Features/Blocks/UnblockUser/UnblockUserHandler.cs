using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Blocks.UnblockUser;

public sealed class UnblockUserHandler : IRequestHandler<UnblockUserCommand>
{
    private readonly IBlockedUserRepository _blocks;
    private readonly ICurrentUser _current;
    private readonly IUnitOfWork _uow;

    public UnblockUserHandler(
        IBlockedUserRepository blocks,
        ICurrentUser current,
        IUnitOfWork uow)
    {
        _blocks = blocks;
        _current = current;
        _uow = uow;
    }

    public async Task Handle(UnblockUserCommand cmd, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        var block = await _blocks.GetAsync(_current.UserId, cmd.BlockedUserId, ct)
                    ?? throw new NotFoundException("Блокировка не найдена.");

        _blocks.Remove(block);
        await _uow.SaveChangesAsync(ct);
    }
}