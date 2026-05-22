namespace InMoment.Domain.Users;

public enum AccountDeletionRequestStatus
{
    Pending = 1,
    InProgress = 2,
    Completed = 3,
    Rejected = 4,
    Cancelled = 5
}