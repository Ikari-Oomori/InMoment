using InMoment.Domain.Common;

namespace InMoment.Domain.Users;

public sealed class User : Entity<Guid>
{
    public string Email { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public string UserName { get; private set; } = default!;
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public string? PhoneNumber { get; private set; }
    public string? ProfilePhotoUrl { get; private set; }
    public string? DeletedEmail { get; private set; }
    public string? DeletedUserName { get; private set; }
    public Guid? ActiveGroupId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsOnboardingCompleted { get; private set; }
    public DateTime? OnboardingCompletedAt { get; private set; }
    public bool HasCompletedContactsStep { get; private set; }
    public bool SkippedContactsImport { get; private set; }

    private User() { }

    public static User Create(
        string email,
        string passwordHash,
        string userName,
        string firstName,
        string lastName,
        string? phoneNumber = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ValidationException("Email is required.");

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ValidationException("PasswordHash is required.");

        if (string.IsNullOrWhiteSpace(userName))
            throw new ValidationException("UserName is required.");

        if (string.IsNullOrWhiteSpace(firstName))
            throw new ValidationException("FirstName is required.");

        if (string.IsNullOrWhiteSpace(lastName))
            throw new ValidationException("LastName is required.");

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedUserName = userName.Trim();
        var normalizedFirstName = firstName.Trim();
        var normalizedLastName = lastName.Trim();
        var normalizedPhoneNumber = PhoneNumberNormalizer.Normalize(phoneNumber);

        if (normalizedEmail.Length > 256)
            throw new ValidationException("Email is too long.");

        if (normalizedUserName.Length > 64)
            throw new ValidationException("UserName is too long.");

        if (normalizedFirstName.Length > 100)
            throw new ValidationException("FirstName is too long.");

        if (normalizedLastName.Length > 100)
            throw new ValidationException("LastName is too long.");

        if (normalizedPhoneNumber is not null && normalizedPhoneNumber.Length > 32)
            throw new ValidationException("PhoneNumber is too long.");

        return new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = passwordHash,
            UserName = normalizedUserName,
            FirstName = normalizedFirstName,
            LastName = normalizedLastName,
            PhoneNumber = normalizedPhoneNumber,
            ProfilePhotoUrl = null,
            ActiveGroupId = null,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            IsOnboardingCompleted = false,
            OnboardingCompletedAt = null,
            HasCompletedContactsStep = false,
            SkippedContactsImport = false
        };
    }

    public void ChangeName(string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ValidationException("FirstName is required.");

        if (string.IsNullOrWhiteSpace(lastName))
            throw new ValidationException("LastName is required.");

        var normalizedFirstName = firstName.Trim();
        var normalizedLastName = lastName.Trim();

        if (normalizedFirstName.Length > 100)
            throw new ValidationException("FirstName is too long.");

        if (normalizedLastName.Length > 100)
            throw new ValidationException("LastName is too long.");

        FirstName = normalizedFirstName;
        LastName = normalizedLastName;
    }

    public void ChangeUserName(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            throw new ValidationException("UserName is required.");

        var normalizedUserName = userName.Trim();

        if (normalizedUserName.Length > 64)
            throw new ValidationException("UserName is too long.");

        UserName = normalizedUserName;
    }

    public void ChangePasswordHash(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ValidationException("Password hash is required.");

        PasswordHash = newPasswordHash;
    }

    public void SetPhoneNumber(string? phoneNumber)
    {
        var normalizedPhoneNumber = PhoneNumberNormalizer.Normalize(phoneNumber);

        if (normalizedPhoneNumber is not null && normalizedPhoneNumber.Length > 32)
            throw new ValidationException("PhoneNumber is too long.");

        PhoneNumber = normalizedPhoneNumber;
    }

    public void SetProfilePhoto(string? profilePhotoUrl)
    {
        ProfilePhotoUrl = string.IsNullOrWhiteSpace(profilePhotoUrl)
            ? null
            : profilePhotoUrl.Trim();
    }

    public void SetActiveGroup(Guid? groupId)
    {
        if (groupId.HasValue && groupId.Value == Guid.Empty)
            throw new ValidationException("ActiveGroupId is invalid.");

        ActiveGroupId = groupId;
    }

    public void MarkContactsStepCompleted(bool skipped)
    {
        HasCompletedContactsStep = true;
        SkippedContactsImport = skipped;
    }

    public void CompleteOnboarding()
    {
        if (!HasCompletedContactsStep)
            throw new ValidationException("Contacts step must be completed before onboarding can be finished.");

        if (IsOnboardingCompleted)
            return;

        IsOnboardingCompleted = true;
        OnboardingCompletedAt = DateTime.UtcNow;
    }

    public void UpdateProfile(string firstName, string lastName, string? profilePhotoUrl)
    {
        ChangeName(firstName, lastName);
        SetProfilePhoto(profilePhotoUrl);
    }

    public void Deactivate()
    {
        if (!IsActive)
            return;

        IsActive = false;
        ActiveGroupId = null;
    }

    public void Reactivate()
    {
        if (IsActive)
            return;

        IsActive = true;
    }
    public void PermanentlyDelete()
    {
        DeletedEmail = Email;
        DeletedUserName = UserName;

        var tombstone = $"deleted_{Id:N}";
        var nowSuffix = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

        Email = $"{tombstone}_{nowSuffix}@deleted.inmoment.local";
        UserName = $"{tombstone}_{nowSuffix}";
        FirstName = "Deleted";
        LastName = "User";
        PhoneNumber = null;
        ProfilePhotoUrl = null;
        ActiveGroupId = null;

        IsActive = false;
        IsOnboardingCompleted = false;
        HasCompletedContactsStep = false;
        SkippedContactsImport = true;
        OnboardingCompletedAt = null;

        PasswordHash = Guid.NewGuid().ToString("N");
    }

    public void EnsureActive()
    {
        if (!IsActive)
            throw new ForbiddenException("Account is deactivated.");
    }
}