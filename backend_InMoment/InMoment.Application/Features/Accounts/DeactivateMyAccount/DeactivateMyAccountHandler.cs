using InMoment.Application.Abstractions.Accounts;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Accounts.DeactivateMyAccount;

public sealed class DeactivateMyAccountHandler : IRequestHandler<DeactivateMyAccountCommand>
{
    private readonly ICurrentUser _current;
    private readonly IAccountDataManager _accounts;

    public DeactivateMyAccountHandler(
        ICurrentUser current,
        IAccountDataManager accounts)
    {
        _current = current;
        _accounts = accounts;
    }

    public async Task Handle(DeactivateMyAccountCommand request, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        await _accounts.DeactivateAccountAsync(_current.UserId, ct);
    }
}