using MediatR;

namespace InMoment.Application.Features.Uploads.PresignGroupAvatarUpload;

public sealed record PresignGroupAvatarUploadCommand(
    Guid GroupId,
    string ContentType
) : IRequest<PresignGroupAvatarUploadResponse>;