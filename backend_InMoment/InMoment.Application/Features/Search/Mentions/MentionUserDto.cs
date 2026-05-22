namespace InMoment.Application.Features.Search.Mentions;

public sealed record MentionUserDto(
    Guid Id,
    string UserName,
    string DisplayName,
    string? ProfilePhotoUrl
);