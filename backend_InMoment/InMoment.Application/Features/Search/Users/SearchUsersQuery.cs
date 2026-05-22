using MediatR;

namespace InMoment.Application.Features.Search.Users;

public sealed record SearchUsersQuery(string Query, int Limit = 10)
    : IRequest<IReadOnlyList<SearchUserDto>>;