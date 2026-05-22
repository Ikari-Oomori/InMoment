using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Storage;
using InMoment.Domain.Media;
using InMoment.Domain.Reports;
using InMoment.Domain.Users;

namespace InMoment.Application.Features.Reports.Common;

public sealed class ReportTargetContextFactory
{
    private readonly IPhotoRepository _photos;
    private readonly ICommentRepository _comments;
    private readonly IUserRepository _users;
    private readonly IGroupRepository _groups;
    private readonly IFileStorage _storage;
    private readonly IReportRepository _reports;

    public ReportTargetContextFactory(
         IPhotoRepository photos,
         ICommentRepository comments,
         IUserRepository users,
         IGroupRepository groups,
         IFileStorage storage,
         IReportRepository reports)
    {
        _photos = photos;
        _comments = comments;
        _users = users;
        _groups = groups;
        _storage = storage;
        _reports = reports;
    }

    public async Task<ReportTargetContextDto> BuildAsync(
        ReportTargetType targetType,
        Guid targetId,
        CancellationToken ct)
    {
        return targetType switch
        {
            ReportTargetType.Photo => await BuildPhotoContextAsync(targetId, ct),
            ReportTargetType.Comment => await BuildCommentContextAsync(targetId, ct),
            ReportTargetType.User => await BuildUserContextAsync(targetId, ct),
            _ => new ReportTargetContextDto(null, null, null)
        };
    }

    private async Task<ReportTargetContextDto> BuildPhotoContextAsync(Guid photoId, CancellationToken ct)
    {
        var photo = await _photos.GetByIdAsync(photoId, ct);
        if (photo is null)
            return new ReportTargetContextDto(null, null, null);

        var author = await _users.GetByIdAsync(photo.UploadedByUserId, ct);
        var group = await _groups.GetByIdAsync(photo.GroupId, ct);

        var photoPreview = new ReportPhotoPreviewDto(
            PhotoId: photo.Id,
            GroupId: photo.GroupId,
            AuthorUserId: photo.UploadedByUserId,
            AuthorUserName: author?.UserName ?? "unknown",
            AuthorDisplayName: BuildDisplayName(author),
            AuthorProfilePhotoUrl: author?.ProfilePhotoUrl,
            GroupName: group?.Name,
            PhotoUrl: photo.IsDeleted ? null : _storage.GetPublicUrl(photo.StorageKey),
            Caption: photo.Caption,
            CreatedAt: photo.CreatedAt,
            IsDeleted: photo.IsDeleted
        );

        return new ReportTargetContextDto(photoPreview, null, null);
    }

    private async Task<ReportTargetContextDto> BuildCommentContextAsync(Guid commentId, CancellationToken ct)
    {
        var comment = await _comments.GetByIdAsync(commentId, ct);
        if (comment is null)
            return new ReportTargetContextDto(null, null, null);

        var commentAuthor = await _users.GetByIdAsync(comment.UserId, ct);
        var photo = await _photos.GetByIdAsync(comment.PhotoId, ct);

        ReportPhotoPreviewDto? photoPreview = null;
        if (photo is not null)
        {
            var photoAuthor = await _users.GetByIdAsync(photo.UploadedByUserId, ct);
            var group = await _groups.GetByIdAsync(photo.GroupId, ct);

            photoPreview = new ReportPhotoPreviewDto(
                PhotoId: photo.Id,
                GroupId: photo.GroupId,
                AuthorUserId: photo.UploadedByUserId,
                AuthorUserName: photoAuthor?.UserName ?? "unknown",
                AuthorDisplayName: BuildDisplayName(photoAuthor),
                AuthorProfilePhotoUrl: photoAuthor?.ProfilePhotoUrl,
                GroupName: group?.Name,
                PhotoUrl: photo.IsDeleted ? null : _storage.GetPublicUrl(photo.StorageKey),
                Caption: photo.Caption,
                CreatedAt: photo.CreatedAt,
                IsDeleted: photo.IsDeleted
            );
        }

        string? parentPreview = null;
        if (comment.ParentCommentId.HasValue)
        {
            var parent = await _comments.GetByIdAsync(comment.ParentCommentId.Value, ct);
            if (parent is not null)
            {
                parentPreview = parent.IsDeleted
                    ? "[удалённый комментарий]"
                    : Truncate(parent.Text, 120);
            }
        }

        var commentPreview = new ReportCommentPreviewDto(
            CommentId: comment.Id,
            PhotoId: comment.PhotoId,
            AuthorUserId: comment.UserId,
            AuthorUserName: commentAuthor?.UserName ?? "unknown",
            AuthorDisplayName: BuildDisplayName(commentAuthor),
            AuthorProfilePhotoUrl: commentAuthor?.ProfilePhotoUrl,
            Text: comment.IsDeleted ? "[удалённый комментарий]" : comment.Text,
            CreatedAt: comment.CreatedAt,
            IsDeleted: comment.IsDeleted,
            ParentCommentId: comment.ParentCommentId,
            ParentCommentTextPreview: parentPreview
        );

        return new ReportTargetContextDto(photoPreview, commentPreview, null);
    }

    private async Task<ReportTargetContextDto> BuildUserContextAsync(Guid userId, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null)
            return new ReportTargetContextDto(null, null, null);

        var totalReports = await _reports.CountByTargetAsync(
            ReportTargetType.User,
            user.Id,
            ct);

        var pendingReports = await _reports.CountByTargetAndStatusAsync(
            ReportTargetType.User,
            user.Id,
            ReportStatus.Pending,
            ct);

        var resolvedReports = await _reports.CountByTargetAndStatusAsync(
            ReportTargetType.User,
            user.Id,
            ReportStatus.Resolved,
            ct);

        var userPreview = new ReportUserPreviewDto(
            UserId: user.Id,
            UserName: user.UserName,
            DisplayName: BuildDisplayName(user),
            ProfilePhotoUrl: user.ProfilePhotoUrl,
            IsActive: user.IsActive,
            CreatedAt: user.CreatedAt,
            ReportsAgainstCount: totalReports,
            PendingReportsAgainstCount: pendingReports,
            ResolvedReportsAgainstCount: resolvedReports
        );

        return new ReportTargetContextDto(null, null, userPreview);
    }

    private static string BuildDisplayName(User? user)
    {
        if (user is null) return "Unknown";
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? user.UserName : fullName;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : trimmed[..maxLength] + "…";
    }
}