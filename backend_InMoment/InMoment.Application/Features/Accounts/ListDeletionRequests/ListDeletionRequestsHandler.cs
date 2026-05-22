using InMoment.Application.Abstractions.Accounts;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Accounts.Common;
using MediatR;

namespace InMoment.Application.Features.Accounts.ListDeletionRequests;

public sealed class ListDeletionRequestsHandler
    : IRequestHandler<ListDeletionRequestsQuery, IReadOnlyList<AccountDeletionRequestDto>>
{
    private readonly ICurrentUser _current;
    private readonly ISystemModeratorAccess _moderatorAccess;
    private readonly IAccountDataManager _accounts;

    public ListDeletionRequestsHandler(
        ICurrentUser current,
        ISystemModeratorAccess moderatorAccess,
        IAccountDataManager accounts)
    {
        _current = current;
        _moderatorAccess = moderatorAccess;
        _accounts = accounts;
    }

    public async Task<IReadOnlyList<AccountDeletionRequestDto>> Handle(
        ListDeletionRequestsQuery request,
        CancellationToken ct)
    {
        _moderatorAccess.EnsureModerator(_current.UserId);

        var limit = Math.Clamp(request.Limit, 1, 200);

        return await _accounts.ListDeletionRequestsAsync(limit, request.Status, ct);
    }
}