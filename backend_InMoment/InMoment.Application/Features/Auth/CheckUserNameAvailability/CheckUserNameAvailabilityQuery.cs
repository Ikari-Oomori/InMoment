using MediatR;

namespace InMoment.Application.Features.Auth.CheckUserNameAvailability;

public sealed record CheckUserNameAvailabilityQuery(string UserName) : IRequest<UserNameAvailabilityDto>;

public sealed record UserNameAvailabilityDto(
    string UserName,
    bool IsAvailable
);