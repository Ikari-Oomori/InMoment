using InMoment.Application.Abstractions.Accounts;
using InMoment.Application.Abstractions.Communication;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Accounts.Common;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Accounts.CreateMyDeletionRequest;

public sealed class CreateMyDeletionRequestHandler
    : IRequestHandler<CreateMyDeletionRequestCommand, AccountDeletionRequestDto>
{
    private readonly ICurrentUser _current;
    private readonly IUserRepository _users;
    private readonly IAccountDataManager _accounts;
    private readonly IAccountDeletionRequestSender _sender;

    public CreateMyDeletionRequestHandler(
        ICurrentUser current,
        IUserRepository users,
        IAccountDataManager accounts,
        IAccountDeletionRequestSender sender)
    {
        _current = current;
        _users = users;
        _accounts = accounts;
        _sender = sender;
    }

    public async Task<AccountDeletionRequestDto> Handle(
        CreateMyDeletionRequestCommand request,
        CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        var user = await _users.GetByIdAsync(_current.UserId, ct)
            ?? throw new NotFoundException("Пользователь не найден.");

        var result = await _accounts.CreateDeletionRequestAsync(
            _current.UserId,
            request.Note,
            ct);

        var displayName = $"{user.FirstName} {user.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = user.UserName;
        }

        await _sender.SendReceivedAsync(
            user.Email,
            displayName,
            ct);

        return result;
    }
}