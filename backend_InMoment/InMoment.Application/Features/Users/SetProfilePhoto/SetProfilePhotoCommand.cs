using MediatR;

namespace InMoment.Application.Features.Users.SetProfilePhoto;

public sealed record SetProfilePhotoCommand(string? Url) : IRequest;