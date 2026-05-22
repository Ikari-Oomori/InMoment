using MediatR;

namespace InMoment.Application.Features.Uploads.PresignPhotoUpload;

public sealed record PresignPhotoUploadCommand(
    Guid GroupId,
    string ContentType
) : IRequest<PresignPhotoUploadResponse>;