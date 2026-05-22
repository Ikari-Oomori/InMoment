using MediatR;

namespace InMoment.Application.Features.Search.Mentions;

public sealed record MentionUsersQuery(
    string Query,
    int Limit = 5,
    Guid? GroupId = null
) : IRequest<IReadOnlyList<MentionUserDto>>;