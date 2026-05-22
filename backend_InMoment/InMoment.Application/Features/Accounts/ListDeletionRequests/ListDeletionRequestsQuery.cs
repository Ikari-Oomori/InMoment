using InMoment.Application.Features.Accounts.Common;
using InMoment.Domain.Users;
using MediatR;

namespace InMoment.Application.Features.Accounts.ListDeletionRequests;

public sealed record ListDeletionRequestsQuery(
    int Limit,
    AccountDeletionRequestStatus? Status) : IRequest<IReadOnlyList<AccountDeletionRequestDto>>;