using InMoment.Domain.Common;

namespace InMoment.Domain.Groups;

public sealed class GroupInvitation : Entity<Guid>
{
    public Guid GroupId { get; private set; }
    public Guid InvitedUserId { get; private set; }
    public Guid InvitedByUserId { get; private set; }
    public InvitationStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? RespondedAt { get; private set; }

    private GroupInvitation() { }

    public static GroupInvitation Create(Guid groupId, Guid invitedUserId, Guid invitedByUserId)
    {
        if (groupId == Guid.Empty) throw new ValidationException("GroupId is required.");
        if (invitedUserId == Guid.Empty) throw new ValidationException("InvitedUserId is required.");
        if (invitedByUserId == Guid.Empty) throw new ValidationException("InvitedByUserId is required.");

        return new GroupInvitation
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            InvitedUserId = invitedUserId,
            InvitedByUserId = invitedByUserId,
            Status = InvitationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Accept()
    {
        if (Status != InvitationStatus.Pending)
            throw new ValidationException("Only pending invitations can be accepted.");

        Status = InvitationStatus.Accepted;
        RespondedAt = DateTime.UtcNow;
    }

    public void Reject()
    {
        if (Status != InvitationStatus.Pending)
            throw new ValidationException("Only pending invitations can be rejected.");

        Status = InvitationStatus.Rejected;
        RespondedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status != InvitationStatus.Pending)
            return;

        Status = InvitationStatus.Cancelled;
        RespondedAt = DateTime.UtcNow;
    }
}