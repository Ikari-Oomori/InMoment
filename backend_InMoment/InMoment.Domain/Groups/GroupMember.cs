using InMoment.Domain.Common;

namespace InMoment.Domain.Groups;

public sealed class GroupMember : Entity<Guid>
{
    public Guid GroupId { get; private set; }
    public Guid UserId { get; private set; }

    public GroupRole Role { get; private set; }

    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private GroupMember() { }

    public static GroupMember CreateOwner(Guid groupId, Guid userId)
        => new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            UserId = userId,
            Role = GroupRole.Owner,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

    public static GroupMember CreateAdmin(Guid groupId, Guid userId)
        => new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            UserId = userId,
            Role = GroupRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

    public static GroupMember CreateMember(Guid groupId, Guid userId)
        => new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            UserId = userId,
            Role = GroupRole.Member,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

    public void SetRole(GroupRole role) => Role = role;

    public void Deactivate()
    {
        IsActive = false;
    }
}