using MediatR;

namespace InMoment.Application.Features.Media.Saved.UnsavePhoto;

public sealed record UnsavePhotoCommand(Guid PhotoId) : IRequest;