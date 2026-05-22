using MediatR;

namespace InMoment.Application.Features.Media.DeletePhoto;

public sealed record DeletePhotoCommand(Guid GroupId, Guid PhotoId) : IRequest;