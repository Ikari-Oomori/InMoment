using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using MediatR;

namespace InMoment.Application.Features.Search.Groups;

public sealed class SearchMyGroupsHandler : IRequestHandler<SearchMyGroupsQuery, IReadOnlyList<SearchGroupDto>>
{
    private readonly IGroupRepository _groups;
    private readonly ICurrentUser _current;

    public SearchMyGroupsHandler(IGroupRepository groups, ICurrentUser current)
    {
        _groups = groups;
        _current = current;
    }

    public async Task<IReadOnlyList<SearchGroupDto>> Handle(SearchMyGroupsQuery q, CancellationToken ct)
    {
        var query = (q.Query ?? string.Empty).Trim();
        if (query.Length == 0)
            return Array.Empty<SearchGroupDto>();

        var limit = q.Limit is < 1 or > 20 ? 10 : q.Limit;

        var items = await _groups.SearchMyGroupsAsync(_current.UserId, query, limit, ct);

        return items.Select(x => new SearchGroupDto(
            x.Id,
            x.Name
        )).ToList();
    }
}