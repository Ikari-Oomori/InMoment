using MediatR;

namespace InMoment.Application.Features.Users.GetWidget;

public sealed record GetWidgetQuery() : IRequest<WidgetDto>;

public sealed record WidgetDto(
    Guid? ActiveGroupId,
    string? ActiveGroupName,
    string? ActiveGroupAvatarUrl,
    Guid? LatestPhotoId,
    string? LatestPhotoUrl,
    DateTime? LatestPhotoCreatedAt,
    int NewReactionsCount
);