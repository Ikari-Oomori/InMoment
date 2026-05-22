namespace InMoment.Infrastructure.Auth;

public sealed class ModerationOptions
{
    public const string SectionName = "Moderation";

    public List<Guid> SystemModeratorUserIds { get; init; } = new();
}