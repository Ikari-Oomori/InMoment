using MediatR;

namespace InMoment.Application.Features.Media.Saved.SavePhoto;

public sealed record SavePhotoCommand(Guid PhotoId) : IRequest;