using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using InMoment.Domain.Groups;
using MediatR;

namespace InMoment.Application.Features.Groups.InviteCodes.Create;

public sealed class CreateInviteCodeHandler : IRequestHandler<CreateInviteCodeCommand, string>
{
    private const int DefaultMaxUses = 1;
    private const int DefaultExpireHours = 168;
    private const int MaxAllowedUses = 20;
    private const int MaxAllowedExpireHours = 720;

    private readonly IGroupRepository _groups;
    private readonly IGroupInviteCodeRepository _codes;
    private readonly ICurrentUser _current;
    private readonly IUnitOfWork _uow;

    public CreateInviteCodeHandler(
        IGroupRepository groups,
        IGroupInviteCodeRepository codes,
        ICurrentUser current,
        IUnitOfWork uow)
    {
        _groups = groups;
        _codes = codes;
        _current = current;
        _uow = uow;
    }

    public async Task<string> Handle(CreateInviteCodeCommand cmd, CancellationToken ct)
    {
        var group = await _groups.GetByIdAsync(cmd.GroupId, ct)
                   ?? throw new NotFoundException("Group not found.");

        group.EnsureManager(_current.UserId);

        var maxUses = cmd.MaxUses ?? DefaultMaxUses;
        if (maxUses <= 0)
            throw new ValidationException("MaxUses must be greater than zero.");

        if (maxUses > MaxAllowedUses)
            throw new ValidationException($"MaxUses must be {MaxAllowedUses} or less.");

        var expireHours = cmd.ExpireHours ?? DefaultExpireHours;
        if (expireHours <= 0)
            throw new ValidationException("ExpireHours must be greater than zero.");

        if (expireHours > MaxAllowedExpireHours)
            throw new ValidationException($"ExpireHours must be {MaxAllowedExpireHours} or less.");

        var now = DateTime.UtcNow;
        var code = GenerateCode();

        var entity = GroupInviteCode.Create(
            groupId: cmd.GroupId,
            code: code,
            createdByUserId: _current.UserId,
            createdAtUtc: now,
            expiresAtUtc: now.AddHours(expireHours),
            maxUses: maxUses);

        await _codes.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);

        return code;
    }

    private static string GenerateCode()
        => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}