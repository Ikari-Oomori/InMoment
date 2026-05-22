using InMoment.Application.Abstractions.Accounts;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Accounts.Common;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Accounts.GetMyDeletionRequest;

public sealed class GetMyDeletionRequestHandler
    : IRequestHandler<GetMyDeletionRequestQuery, AccountDeletionRequestDto?>
{
    private readonly ICurrentUser _current;
    private readonly IAccountDataManager _accounts;

    public GetMyDeletionRequestHandler(
        ICurrentUser current,
        IAccountDataManager accounts)
    {
        _current = current;
        _accounts = accounts;
    }

    public async Task<AccountDeletionRequestDto?> Handle(GetMyDeletionRequestQuery request, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        return await _accounts.GetLatestDeletionRequestAsync(_current.UserId, ct);
    }
}