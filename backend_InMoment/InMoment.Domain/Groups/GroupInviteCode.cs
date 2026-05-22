using InMoment.Domain.Common;

namespace InMoment.Domain.Groups;

public sealed class GroupInviteCode : Entity<Guid>
{
    public Guid GroupId { get; private set; }

    public string Code { get; private set; } = default!;

    public Guid CreatedByUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ExpiresAtUtc { get; private set; }

    public int? MaxUses { get; private set; }

    public int UsesCount { get; private set; }

    public bool IsRevoked { get; private set; }

    private GroupInviteCode() { }

    public static GroupInviteCode Create(
        Guid groupId,
        string code,
        Guid createdByUserId,
        DateTime createdAtUtc,
        DateTime? expiresAtUtc,
        int? maxUses)
    {
        if (groupId == Guid.Empty)
            throw new ValidationException("GroupId is required.");

        if (string.IsNullOrWhiteSpace(code))
            throw new ValidationException("Code is required.");

        return new GroupInviteCode
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            Code = code,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = expiresAtUtc,
            MaxUses = maxUses,
            UsesCount = 0,
            IsRevoked = false
        };
    }

    public void EnsureUsable(DateTime now)
    {
        if (IsRevoked)
            throw new ForbiddenException("Invite code revoked.");

        if (ExpiresAtUtc.HasValue && ExpiresAtUtc < now)
            throw new ForbiddenException("Invite code expired.");

        if (MaxUses.HasValue && UsesCount >= MaxUses)
            throw new ForbiddenException("Invite code usage limit reached.");
    }

    public void Use()
    {
        UsesCount++;
    }

    public void Revoke()
    {
        IsRevoked = true;
    }
}