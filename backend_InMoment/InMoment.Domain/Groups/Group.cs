using InMoment.Domain.Common;

namespace InMoment.Domain.Groups;

public sealed class Group : Entity<Guid>
{
    public Guid OwnerId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public string? AvatarUrl { get; private set; }

    public Guid CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public bool IsActive { get; private set; } = true;

    private readonly List<GroupMember> _members = new();
    public IReadOnlyCollection<GroupMember> Members => _members;

    private Group() { }

    public static Group Create(string name, Guid ownerUserId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Group name is required.");

        if (ownerUserId == Guid.Empty)
            throw new ValidationException("OwnerId is required.");

        var group = new Group
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerUserId,
            Name = name.Trim(),
            Description = null,
            AvatarUrl = null,
            CreatedBy = ownerUserId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        group._members.Add(GroupMember.CreateOwner(group.Id, ownerUserId));
        return group;
    }

    public void UpdateSettings(Guid actorUserId, string name, string? description)
    {
        EnsureOwner(actorUserId);

        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Group name is required.");

        var normalizedName = name.Trim();
        if (normalizedName.Length > 100)
            throw new ValidationException("Group name must be 100 characters or less.");

        string? normalizedDescription = null;
        if (!string.IsNullOrWhiteSpace(description))
        {
            normalizedDescription = description.Trim();

            if (normalizedDescription.Length > 500)
                throw new ValidationException("Group description must be 500 characters or less.");
        }

        Name = normalizedName;
        Description = normalizedDescription;
    }

    public void SetAvatar(Guid actorUserId, string? avatarUrl)
    {
        EnsureOwner(actorUserId);

        AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl)
            ? null
            : avatarUrl.Trim();
    }

    public void EnsureOwner(Guid userId)
    {
        if (!IsActive)
            throw new ForbiddenException("Group is inactive.");

        if (OwnerId != userId)
            throw new ForbiddenException("Only owner can perform this action.");

        var hasOwnerMember = _members.Any(m => m.IsActive && m.UserId == userId && m.Role == GroupRole.Owner);
        if (!hasOwnerMember)
            throw new ForbiddenException("Owner membership is missing.");
    }

    public void EnsureManager(Guid userId)
    {
        if (!IsActive)
            throw new ForbiddenException("Group is inactive.");

        var member = _members.FirstOrDefault(m => m.IsActive && m.UserId == userId);
        if (member is null)
            throw new ForbiddenException("You are not a member of this group.");

        if (member.Role is not (GroupRole.Owner or GroupRole.Admin))
            throw new ForbiddenException("Only owner or admin can perform this action.");
    }

    public void EnsureMember(Guid userId)
    {
        if (!IsActive)
            throw new ForbiddenException("Group is inactive.");

        if (!_members.Any(m => m.IsActive && m.UserId == userId))
            throw new ForbiddenException("You are not a member of this group.");
    }

    public bool IsMember(Guid userId)
        => _members.Any(m => m.IsActive && m.UserId == userId);

    public bool IsAdmin(Guid userId)
        => _members.Any(m => m.IsActive && m.UserId == userId && m.Role == GroupRole.Admin);

    public bool IsManager(Guid userId)
        => _members.Any(m => m.IsActive && m.UserId == userId && (m.Role == GroupRole.Owner || m.Role == GroupRole.Admin));

    public void AddMember(Guid userId)
    {
        if (!IsActive)
            throw new ForbiddenException("Group is inactive.");

        if (userId == Guid.Empty)
            throw new ValidationException("UserId is required.");

        if (_members.Any(m => m.IsActive && m.UserId == userId))
            return;

        _members.Add(GroupMember.CreateMember(Id, userId));
    }

    public void PromoteToAdmin(Guid actingUserId, Guid targetUserId)
    {
        EnsureOwner(actingUserId);

        if (targetUserId == Guid.Empty)
            throw new ValidationException("TargetUserId is required.");

        if (targetUserId == actingUserId)
            throw new ValidationException("Owner cannot promote himself.");

        var target = _members.FirstOrDefault(m => m.IsActive && m.UserId == targetUserId);
        if (target is null)
            throw new ValidationException("Target user is not an active member of this group.");

        if (target.Role == GroupRole.Owner)
            throw new ValidationException("Owner is already the highest role.");

        if (target.Role == GroupRole.Admin)
            return;

        target.SetRole(GroupRole.Admin);
    }

    public void DemoteAdmin(Guid actingUserId, Guid targetUserId)
    {
        EnsureOwner(actingUserId);

        if (targetUserId == Guid.Empty)
            throw new ValidationException("TargetUserId is required.");

        var target = _members.FirstOrDefault(m => m.IsActive && m.UserId == targetUserId);
        if (target is null)
            throw new ValidationException("Target user is not an active member of this group.");

        if (target.Role == GroupRole.Owner)
            throw new ValidationException("Owner cannot be demoted.");

        if (target.Role == GroupRole.Member)
            return;

        target.SetRole(GroupRole.Member);
    }

    public void TransferOwnership(Guid currentOwnerId, Guid newOwnerUserId)
    {
        EnsureOwner(currentOwnerId);

        if (newOwnerUserId == Guid.Empty)
            throw new ValidationException("NewOwnerUserId is required.");

        if (newOwnerUserId == currentOwnerId)
            throw new ValidationException("New owner must be different from current owner.");

        var newOwner = _members.FirstOrDefault(m => m.IsActive && m.UserId == newOwnerUserId);
        if (newOwner is null)
            throw new ValidationException("New owner must be a member of the group.");

        var currentOwner = _members.First(m => m.IsActive && m.UserId == currentOwnerId && m.Role == GroupRole.Owner);

        currentOwner.SetRole(GroupRole.Admin);
        newOwner.SetRole(GroupRole.Owner);

        OwnerId = newOwnerUserId;
    }

    public void DemoteOwnerToAdmin(Guid currentOwnerId, Guid newOwnerUserId)
    {
        EnsureOwner(currentOwnerId);

        if (newOwnerUserId == Guid.Empty)
            throw new ValidationException("NewOwnerUserId is required.");

        if (newOwnerUserId == currentOwnerId)
            throw new ValidationException("New owner must be different from current owner.");

        var newOwner = _members.FirstOrDefault(m => m.IsActive && m.UserId == newOwnerUserId);
        if (newOwner is null)
            throw new ValidationException("New owner must be a member of the group.");

        var currentOwner = _members.First(m => m.IsActive && m.UserId == currentOwnerId && m.Role == GroupRole.Owner);
        currentOwner.SetRole(GroupRole.Admin);
    }

    public void PromoteAdminOrMemberToOwner(Guid currentOwnerId, Guid newOwnerUserId)
    {
        if (!IsActive)
            throw new ForbiddenException("Group is inactive.");

        if (newOwnerUserId == Guid.Empty)
            throw new ValidationException("NewOwnerUserId is required.");

        if (newOwnerUserId == currentOwnerId)
            throw new ValidationException("New owner must be different from current owner.");

        var currentOwner = _members.FirstOrDefault(m => m.IsActive && m.UserId == currentOwnerId && m.Role == GroupRole.Owner);
        if (currentOwner is not null)
            throw new ValidationException("Current owner must be demoted before promoting a new owner.");

        var newOwner = _members.FirstOrDefault(m => m.IsActive && m.UserId == newOwnerUserId);
        if (newOwner is null)
            throw new ValidationException("New owner must be a member of the group.");

        newOwner.SetRole(GroupRole.Owner);
        OwnerId = newOwnerUserId;
    }

    public void Leave(Guid userId)
    {
        if (!IsActive)
            throw new ForbiddenException("Group is inactive.");

        var member = _members.FirstOrDefault(m => m.IsActive && m.UserId == userId);
        if (member is null)
            return;

        if (member.Role == GroupRole.Owner)
        {
            var otherActiveMembers = _members.Count(m => m.IsActive && m.UserId != userId);
            if (otherActiveMembers > 0)
                throw new ValidationException("Owner must transfer ownership before leaving.");

            IsActive = false;
        }

        member.Deactivate();
    }

    public void RemoveMember(Guid actingUserId, Guid targetUserId)
    {
        EnsureManager(actingUserId);

        if (targetUserId == Guid.Empty)
            throw new ValidationException("TargetUserId is required.");

        if (targetUserId == actingUserId)
            throw new ValidationException("You cannot remove yourself. Use Leave.");

        var actor = _members.First(m => m.IsActive && m.UserId == actingUserId);
        var target = _members.FirstOrDefault(m => m.IsActive && m.UserId == targetUserId);

        if (target is null)
            throw new ValidationException("Target user is not an active member of this group.");

        if (target.Role == GroupRole.Owner)
            throw new ValidationException("Cannot remove owner. Transfer ownership first.");

        if (actor.Role == GroupRole.Admin && target.Role != GroupRole.Member)
            throw new ForbiddenException("Admin can remove only regular members.");

        target.Deactivate();
    }

    public void Delete(Guid actingUserId)
    {
        EnsureOwner(actingUserId);

        if (!IsActive)
            return;

        IsActive = false;

        foreach (var member in _members.Where(m => m.IsActive))
            member.Deactivate();
    }
}