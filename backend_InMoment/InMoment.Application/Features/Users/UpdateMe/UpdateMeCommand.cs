using MediatR;

namespace InMoment.Application.Features.Users.UpdateMe;

public sealed record UpdateMeCommand(
    string? UserName,
    string? FirstName,
    string? LastName,
    string? PhoneNumber = null
) : IRequest<UpdatedMeDto>;

public sealed record UpdatedMeDto(
    Guid Id,
    string Email,
    string UserName,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? ProfilePhotoUrl,
    Guid? ActiveGroupId,
    DateTime CreatedAt
);