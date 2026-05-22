using InMoment.Application.Abstractions.Accounts;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Accounts.Common;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Accounts.GetMyDataSummary;

public sealed class GetMyDataSummaryHandler
    : IRequestHandler<GetMyDataSummaryQuery, AccountDataSummaryDto>
{
    private readonly ICurrentUser _current;
    private readonly IAccountDataManager _accounts;

    public GetMyDataSummaryHandler(
        ICurrentUser current,
        IAccountDataManager accounts)
    {
        _current = current;
        _accounts = accounts;
    }

    public async Task<AccountDataSummaryDto> Handle(GetMyDataSummaryQuery request, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        return await _accounts.GetSummaryAsync(_current.UserId, ct);
    }
}