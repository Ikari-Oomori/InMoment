using InMoment.Domain.Common;

namespace InMoment.Domain.Contacts;

public sealed class ContactInvite : Entity<Guid>
{
    public Guid UserId { get; private set; }

    public ContactInviteChannel Channel { get; private set; }

    public string? Email { get; private set; }
    public string? PhoneNumber { get; private set; }

    public string? DisplayName { get; private set; }
    public string InviteToken { get; private set; } = default!;

    public ContactInviteStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }

    private ContactInvite() { }

    public static ContactInvite CreateEmail(
        Guid userId,
        string email,
        string? displayName,
        string inviteToken)
    {
        if (userId == Guid.Empty)
            throw new ValidationException("UserId is required.");

        var normalizedEmail = NormalizeEmail(email);
        var normalizedDisplayName = NormalizeDisplayName(displayName);
        var normalizedInviteToken = NormalizeInviteToken(inviteToken);

        return new ContactInvite
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Channel = ContactInviteChannel.Email,
            Email = normalizedEmail,
            PhoneNumber = null,
            DisplayName = normalizedDisplayName,
            InviteToken = normalizedInviteToken,
            Status = ContactInviteStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            CancelledAtUtc = null
        };
    }

    public static ContactInvite CreateSms(
        Guid userId,
        string phoneNumber,
        string? displayName,
        string inviteToken)
    {
        if (userId == Guid.Empty)
            throw new ValidationException("UserId is required.");

        var normalizedPhone = PhoneNumberNormalizer.Normalize(phoneNumber)
                              ?? throw new ValidationException("PhoneNumber is required.");

        if (normalizedPhone.Length > 32)
            throw new ValidationException("PhoneNumber is too long.");

        var normalizedDisplayName = NormalizeDisplayName(displayName);
        var normalizedInviteToken = NormalizeInviteToken(inviteToken);

        return new ContactInvite
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Channel = ContactInviteChannel.Sms,
            Email = null,
            PhoneNumber = normalizedPhone,
            DisplayName = normalizedDisplayName,
            InviteToken = normalizedInviteToken,
            Status = ContactInviteStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            CancelledAtUtc = null
        };
    }

    public void Cancel()
    {
        if (Status == ContactInviteStatus.Cancelled)
            return;

        Status = ContactInviteStatus.Cancelled;
        CancelledAtUtc = DateTime.UtcNow;
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ValidationException("Email is required.");

        var normalized = email.Trim().ToLowerInvariant();

        if (normalized.Length > 256)
            throw new ValidationException("Email is too long.");

        return normalized;
    }

    private static string NormalizeInviteToken(string inviteToken)
    {
        if (string.IsNullOrWhiteSpace(inviteToken))
            throw new ValidationException("InviteToken is required.");

        var normalized = inviteToken.Trim();

        if (normalized.Length > 200)
            throw new ValidationException("InviteToken is too long.");

        return normalized;
    }

    private static string? NormalizeDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return null;

        var normalized = displayName.Trim();

        if (normalized.Length > 200)
            throw new ValidationException("DisplayName is too long.");

        return normalized;
    }
}