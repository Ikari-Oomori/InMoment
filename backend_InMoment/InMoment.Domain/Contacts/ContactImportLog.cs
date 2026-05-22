using InMoment.Domain.Common;

namespace InMoment.Domain.Contacts;

public sealed class ContactImportLog : Entity<Guid>
{
    private ContactImportLog() { }

    public Guid UserId { get; private set; }
    public int ContactsSubmitted { get; private set; }
    public int MatchesFound { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static ContactImportLog Create(Guid userId, int contactsSubmitted, int matchesFound)
    {
        if (userId == Guid.Empty)
            throw new ValidationException("UserId is required.");

        if (contactsSubmitted < 0)
            throw new ValidationException("ContactsSubmitted must be >= 0.");

        if (matchesFound < 0)
            throw new ValidationException("MatchesFound must be >= 0.");

        return new ContactImportLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ContactsSubmitted = contactsSubmitted,
            MatchesFound = matchesFound,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
