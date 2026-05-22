using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Friends.RejectRequest;

public sealed class RejectFriendRequestHandler : IRequestHandler<RejectFriendRequestCommand>
{
    private readonly IFriendRequestRepository _requests;
    private readonly ICurrentUser _current;
    private readonly IUnitOfWork _uow;

    public RejectFriendRequestHandler(
        IFriendRequestRepository requests,
        ICurrentUser current,
        IUnitOfWork uow)
    {
        _requests = requests;
        _current = current;
        _uow = uow;
    }

    public async Task Handle(RejectFriendRequestCommand cmd, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        var request = await _requests.GetByIdAsync(cmd.RequestId, ct)
                      ?? throw new NotFoundException("Заявка не найдена.");

        if (request.ToUserId != _current.UserId)
            throw new ForbiddenException("Можно отклонить только входящую заявку.");

        request.Reject();
        await _uow.SaveChangesAsync(ct);
    }
}