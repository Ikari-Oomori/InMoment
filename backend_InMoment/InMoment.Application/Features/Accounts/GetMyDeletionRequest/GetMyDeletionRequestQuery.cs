using InMoment.Application.Features.Accounts.Common;
using MediatR;

namespace InMoment.Application.Features.Accounts.GetMyDeletionRequest;

public sealed record GetMyDeletionRequestQuery : IRequest<AccountDeletionRequestDto?>;