using InMoment.Application.Abstractions.Accounts;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Accounts.Common;
using InMoment.Domain.Common;
using InMoment.Domain.Users;
using MediatR;

namespace InMoment.Application.Features.Accounts.ReviewDeletionRequest;

public sealed class ReviewDeletionRequestHandler
    : IRequestHandler<ReviewDeletionRequestCommand, AccountDeletionRequestDto>
{
    private readonly ICurrentUser _current;
    private readonly ISystemModeratorAccess _moderatorAccess;
    private readonly IAccountDataManager _accounts;

    public ReviewDeletionRequestHandler(
        ICurrentUser current,
        ISystemModeratorAccess moderatorAccess,
        IAccountDataManager accounts)
    {
        _current = current;
        _moderatorAccess = moderatorAccess;
        _accounts = accounts;
    }

    public async Task<AccountDeletionRequestDto> Handle(
        ReviewDeletionRequestCommand request,
        CancellationToken ct)
    {
        _moderatorAccess.EnsureModerator(_current.UserId);

        if (request.Status is not AccountDeletionRequestStatus.InProgress
            and not AccountDeletionRequestStatus.Rejected
            and not AccountDeletionRequestStatus.Completed)
        {
            throw new ValidationException(
                "Для модерации доступны только статусы InProgress, Rejected и Completed.");
        }

        if (request.Status == AccountDeletionRequestStatus.Completed &&
            !request.PermanentlyDeleteNow)
        {
            throw new ValidationException(
                "Для завершения запроса необходимо выполнить фактическое удаление аккаунта.");
        }

        return await _accounts.ReviewDeletionRequestAsync(
            request.RequestId,
            _current.UserId,
            request.Status,
            request.ProcessingNote,
            request.PermanentlyDeleteNow,
            ct);
    }
}