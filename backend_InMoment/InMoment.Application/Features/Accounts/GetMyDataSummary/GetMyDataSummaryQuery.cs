using InMoment.Application.Features.Accounts.Common;
using MediatR;

namespace InMoment.Application.Features.Accounts.GetMyDataSummary;

public sealed record GetMyDataSummaryQuery : IRequest<AccountDataSummaryDto>;