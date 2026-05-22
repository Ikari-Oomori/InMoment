using MediatR;

namespace InMoment.Application.Features.Uploads.PresignSystemAnnouncementMediaUpload;

public sealed record PresignSystemAnnouncementMediaUploadCommand(
    string ContentType
) : IRequest<PresignSystemAnnouncementMediaUploadResponse>;