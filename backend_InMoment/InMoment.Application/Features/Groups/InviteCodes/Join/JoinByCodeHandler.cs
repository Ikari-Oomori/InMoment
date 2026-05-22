using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using MediatR;

namespace InMoment.Application.Features.Groups.InviteCodes.Join;

public sealed class JoinByCodeHandler : IRequestHandler<JoinByCodeCommand>
{
    private readonly IGroupInviteCodeRepository _codes;
    private readonly IGroupRepository _groups;
    private readonly ICurrentUser _current;
    private readonly IUnitOfWork _uow;

    public JoinByCodeHandler(
        IGroupInviteCodeRepository codes,
        IGroupRepository groups,
        ICurrentUser current,
        IUnitOfWork uow)
    {
        _codes = codes;
        _groups = groups;
        _current = current;
        _uow = uow;
    }

    public async Task Handle(JoinByCodeCommand cmd, CancellationToken ct)
    {
        var normalizedCode = (cmd.Code ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedCode))
            throw new ValidationException("Code is required.");

        await using var tx = await _uow.BeginTransactionAsync(ct);

        var inviteCode = await _codes.GetByCodeAsync(normalizedCode, ct)
                         ?? throw new NotFoundException("Invite code not found.");

        inviteCode.EnsureUsable(DateTime.UtcNow);

        var group = await _groups.GetByIdAsync(inviteCode.GroupId, ct)
                   ?? throw new NotFoundException("Group not found.");

        if (!group.IsActive)
            throw new ForbiddenException("Group is inactive.");

        if (group.IsMember(_current.UserId))
        {
            await tx.CommitAsync(ct);
            return;
        }

        group.AddMember(_current.UserId);
        inviteCode.Use();

        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}