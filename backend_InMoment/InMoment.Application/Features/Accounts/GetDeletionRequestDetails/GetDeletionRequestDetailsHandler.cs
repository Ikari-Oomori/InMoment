using InMoment.Application.Abstractions.Accounts;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Accounts.Common;
using MediatR;

namespace InMoment.Application.Features.Accounts.GetDeletionRequestDetails;

public sealed class GetDeletionRequestDetailsHandler
    : IRequestHandler<GetDeletionRequestDetailsQuery, AccountDeletionRequestDto>
{
    private readonly ICurrentUser _current;
    private readonly ISystemModeratorAccess _moderatorAccess;
    private readonly IAccountDataManager _accounts;

    public GetDeletionRequestDetailsHandler(
        ICurrentUser current,
        ISystemModeratorAccess moderatorAccess,
        IAccountDataManager accounts)
    {
        _current = current;
        _moderatorAccess = moderatorAccess;
        _accounts = accounts;
    }

    public async Task<AccountDeletionRequestDto> Handle(
        GetDeletionRequestDetailsQuery request,
        CancellationToken ct)
    {
        _moderatorAccess.EnsureModerator(_current.UserId);

        return await _accounts.GetDeletionRequestByIdAsync(request.RequestId, ct);
    }
}