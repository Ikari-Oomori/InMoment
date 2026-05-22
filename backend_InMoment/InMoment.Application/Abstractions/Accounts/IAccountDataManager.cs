using InMoment.Application.Features.Accounts.Common;
using InMoment.Domain.Users;

namespace InMoment.Application.Abstractions.Accounts;

public interface IAccountDataManager
{
    Task<AccountDataSummaryDto> GetSummaryAsync(Guid userId, CancellationToken ct);

    Task<AccountDeletionRequestDto?> GetLatestDeletionRequestAsync(Guid userId, CancellationToken ct);

    Task<AccountDeletionRequestDto> CreateDeletionRequestAsync(Guid userId, string? note, CancellationToken ct);

    Task<IReadOnlyList<AccountDeletionRequestDto>> ListDeletionRequestsAsync(
        int limit,
        AccountDeletionRequestStatus? status,
        CancellationToken ct);

    Task<AccountDeletionRequestDto> GetDeletionRequestByIdAsync(Guid requestId, CancellationToken ct);

    Task<AccountDeletionRequestDto> ReviewDeletionRequestAsync(
        Guid requestId,
        Guid moderatorUserId,
        AccountDeletionRequestStatus status,
        string? processingNote,
        bool permanentlyDeleteNow,
        CancellationToken ct);

    Task DeactivateAccountAsync(Guid userId, CancellationToken ct);

    Task PermanentlyDeleteAccountAsync(Guid userId, CancellationToken ct);
}