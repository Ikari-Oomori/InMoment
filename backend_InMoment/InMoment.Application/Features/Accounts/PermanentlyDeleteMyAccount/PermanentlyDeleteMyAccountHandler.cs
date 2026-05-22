using InMoment.Application.Abstractions.Accounts;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Accounts.PermanentlyDeleteMyAccount;

public sealed class PermanentlyDeleteMyAccountHandler
    : IRequestHandler<PermanentlyDeleteMyAccountCommand>
{
    private readonly ICurrentUser _current;
    private readonly IAccountDataManager _accounts;

    public PermanentlyDeleteMyAccountHandler(
        ICurrentUser current,
        IAccountDataManager accounts)
    {
        _current = current;
        _accounts = accounts;
    }

    public async Task Handle(PermanentlyDeleteMyAccountCommand request, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        if (!string.Equals(request.Confirmation?.Trim(), "DELETE", StringComparison.Ordinal))
            throw new ValidationException("Неверное подтверждение удаления аккаунта.");

        await _accounts.PermanentlyDeleteAccountAsync(_current.UserId, ct);
    }
}