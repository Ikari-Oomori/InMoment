using InMoment.Application.Features.Accounts.Common;
using MediatR;

namespace InMoment.Application.Features.Accounts.CreateMyDeletionRequest;

public sealed record CreateMyDeletionRequestCommand(string? Note) : IRequest<AccountDeletionRequestDto>;