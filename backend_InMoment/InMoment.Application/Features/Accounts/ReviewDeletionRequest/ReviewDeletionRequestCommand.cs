using InMoment.Application.Features.Accounts.Common;
using InMoment.Domain.Users;
using MediatR;

namespace InMoment.Application.Features.Accounts.ReviewDeletionRequest;

public sealed record ReviewDeletionRequestCommand(
    Guid RequestId,
    AccountDeletionRequestStatus Status,
    string? ProcessingNote,
    bool PermanentlyDeleteNow) : IRequest<AccountDeletionRequestDto>;