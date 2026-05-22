using InMoment.Application.Abstractions.Accounts;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Accounts.DeleteMyAccount;

public sealed class DeleteMyAccountHandler : IRequestHandler<DeleteMyAccountCommand>
{
    private const string RequiredConfirmation = "DELETE";

    private readonly ICurrentUser _current;
    private readonly IAccountDataManager _accounts;

    public DeleteMyAccountHandler(
        ICurrentUser current,
        IAccountDataManager accounts)
    {
        _current = current;
        _accounts = accounts;
    }

    public async Task Handle(DeleteMyAccountCommand request, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        var confirmation = (request.Confirmation ?? string.Empty).Trim();

        if (!string.Equals(confirmation, RequiredConfirmation, StringComparison.Ordinal))
            throw new ValidationException("Для удаления аккаунта подтвердите действие строкой DELETE.");

        await _accounts.DeactivateAccountAsync(_current.UserId, ct);
    }
}