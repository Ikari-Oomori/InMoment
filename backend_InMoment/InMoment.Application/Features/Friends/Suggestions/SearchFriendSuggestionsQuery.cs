using MediatR;

namespace InMoment.Application.Features.Friends.Suggestions;

public sealed record SearchFriendSuggestionsQuery(
    string Query,
    int Limit = 10
) : IRequest<IReadOnlyList<FriendSuggestionDto>>;