using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using Microsoft.Extensions.Options;

namespace InMoment.Infrastructure.Auth;

public sealed class ConfiguredSystemModeratorAccess : ISystemModeratorAccess
{
    private readonly HashSet<Guid> _moderatorIds;

    public ConfiguredSystemModeratorAccess(IOptions<ModerationOptions> options)
    {
        var value = options.Value ?? new ModerationOptions();

        _moderatorIds = value.SystemModeratorUserIds
            .Where(x => x != Guid.Empty)
            .ToHashSet();
    }

    public bool IsModerator(Guid userId)
    {
        if (userId == Guid.Empty)
            return false;

        return _moderatorIds.Contains(userId);
    }

    public void EnsureModerator(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        if (!_moderatorIds.Contains(userId))
            throw new ForbiddenException("Доступ разрешён только системному модератору.");
    }
}