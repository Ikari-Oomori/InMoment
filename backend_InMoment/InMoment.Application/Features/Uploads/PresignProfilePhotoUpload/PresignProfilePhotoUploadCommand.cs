using MediatR;

namespace InMoment.Application.Features.Uploads.PresignProfilePhotoUpload;

public sealed record PresignProfilePhotoUploadCommand(string ContentType)
    : IRequest<PresignProfilePhotoUploadResponse>;