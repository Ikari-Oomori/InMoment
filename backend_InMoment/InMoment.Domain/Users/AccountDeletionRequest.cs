using InMoment.Domain.Common;

namespace InMoment.Domain.Users;

public sealed class AccountDeletionRequest : Entity<Guid>
{
    public Guid UserId { get; private set; }

    public string RequestedEmail { get; private set; } = default!;
    public string RequestedUserName { get; private set; } = default!;

    public AccountDeletionRequestStatus Status { get; private set; }

    public string? Note { get; private set; }
    public string? ProcessingNote { get; private set; }

    public DateTime RequestedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public Guid? ProcessedByUserId { get; private set; }

    private AccountDeletionRequest() { }

    public static AccountDeletionRequest Create(
        Guid userId,
        string requestedEmail,
        string requestedUserName,
        string? note)
    {
        if (userId == Guid.Empty)
            throw new ValidationException("UserId is required.");

        var normalizedEmail = NormalizeEmail(requestedEmail);
        var normalizedUserName = NormalizeUserName(requestedUserName);
        var normalizedNote = NormalizeNote(note);

        var now = DateTime.UtcNow;

        return new AccountDeletionRequest
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RequestedEmail = normalizedEmail,
            RequestedUserName = normalizedUserName,
            Status = AccountDeletionRequestStatus.Pending,
            Note = normalizedNote,
            ProcessingNote = null,
            RequestedAtUtc = now,
            UpdatedAtUtc = now,
            ProcessedAtUtc = null,
            ProcessedByUserId = null
        };
    }

    public void MarkInProgress(Guid moderatorUserId, string? processingNote = null)
    {
        EnsureModerator(moderatorUserId);
        EnsureNotTerminal();

        Status = AccountDeletionRequestStatus.InProgress;
        ProcessingNote = NormalizeProcessingNote(processingNote) ?? ProcessingNote;
        UpdatedAtUtc = DateTime.UtcNow;
        ProcessedByUserId = moderatorUserId;
    }

    public void Complete(Guid moderatorUserId, string? processingNote = null)
    {
        EnsureModerator(moderatorUserId);
        EnsureNotTerminal();

        Status = AccountDeletionRequestStatus.Completed;
        ProcessingNote = NormalizeProcessingNote(processingNote) ?? ProcessingNote;
        UpdatedAtUtc = DateTime.UtcNow;
        ProcessedAtUtc = DateTime.UtcNow;
        ProcessedByUserId = moderatorUserId;
    }

    public void Reject(Guid moderatorUserId, string? processingNote = null)
    {
        EnsureModerator(moderatorUserId);
        EnsureNotTerminal();

        Status = AccountDeletionRequestStatus.Rejected;
        ProcessingNote = NormalizeProcessingNote(processingNote) ?? ProcessingNote;
        UpdatedAtUtc = DateTime.UtcNow;
        ProcessedAtUtc = DateTime.UtcNow;
        ProcessedByUserId = moderatorUserId;
    }

    public void Cancel(string? note = null)
    {
        EnsureNotTerminal();

        Status = AccountDeletionRequestStatus.Cancelled;
        Note = NormalizeNote(note) ?? Note;
        UpdatedAtUtc = DateTime.UtcNow;
        ProcessedAtUtc = DateTime.UtcNow;
    }

    private void EnsureNotTerminal()
    {
        if (Status is AccountDeletionRequestStatus.Completed
            or AccountDeletionRequestStatus.Rejected
            or AccountDeletionRequestStatus.Cancelled)
        {
            throw new ValidationException("Request is already finalized.");
        }
    }

    private static void EnsureModerator(Guid moderatorUserId)
    {
        if (moderatorUserId == Guid.Empty)
            throw new ValidationException("Moderator user id is required.");
    }

    private static string NormalizeEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationException("RequestedEmail is required.");

        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length > 256)
            throw new ValidationException("RequestedEmail is too long.");

        return normalized;
    }

    private static string NormalizeUserName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationException("RequestedUserName is required.");

        var normalized = value.Trim();

        if (normalized.Length > 64)
            throw new ValidationException("RequestedUserName is too long.");

        return normalized;
    }

    private static string? NormalizeNote(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();

        if (normalized.Length > 2000)
            throw new ValidationException("Note is too long.");

        return normalized;
    }

    private static string? NormalizeProcessingNote(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();

        if (normalized.Length > 2000)
            throw new ValidationException("ProcessingNote is too long.");

        return normalized;
    }
}