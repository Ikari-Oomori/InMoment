using InMoment.Domain.Common;

namespace InMoment.Application.Abstractions.Security;

public interface ISystemModeratorAccess
{
    bool IsModerator(Guid userId);

    void EnsureModerator(Guid userId);
}