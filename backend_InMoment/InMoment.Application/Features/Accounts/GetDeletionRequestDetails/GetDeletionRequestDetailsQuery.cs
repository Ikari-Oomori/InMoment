using InMoment.Application.Features.Accounts.Common;
using MediatR;

namespace InMoment.Application.Features.Accounts.GetDeletionRequestDetails;

public sealed record GetDeletionRequestDetailsQuery(Guid RequestId)
    : IRequest<AccountDeletionRequestDto>;