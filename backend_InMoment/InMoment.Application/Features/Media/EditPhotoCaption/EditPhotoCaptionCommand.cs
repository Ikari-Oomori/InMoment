using MediatR;

namespace InMoment.Application.Features.Media.EditPhotoCaption;

public sealed record EditPhotoCaptionCommand(
    Guid GroupId,
    Guid PhotoId,
    string? Caption
) : IRequest<Guid>;