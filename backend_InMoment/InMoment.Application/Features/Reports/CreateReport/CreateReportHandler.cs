using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Domain.Common;
using InMoment.Domain.Reports;
using MediatR;

namespace InMoment.Application.Features.Reports.CreateReport;

public sealed class CreateReportHandler : IRequestHandler<CreateReportCommand, Guid>
{
    private readonly IReportRepository _reports;
    private readonly IPhotoRepository _photos;
    private readonly ICommentRepository _comments;
    private readonly IUserRepository _users;
    private readonly ICurrentUser _current;
    private readonly IUnitOfWork _uow;

    public CreateReportHandler(
        IReportRepository reports,
        IPhotoRepository photos,
        ICommentRepository comments,
        IUserRepository users,
        ICurrentUser current,
        IUnitOfWork uow)
    {
        _reports = reports;
        _photos = photos;
        _comments = comments;
        _users = users;
        _current = current;
        _uow = uow;
    }

    public async Task<Guid> Handle(CreateReportCommand cmd, CancellationToken ct)
    {
        if (_current.UserId == Guid.Empty)
            throw new ForbiddenException("Пользователь не авторизован.");

        if (!Enum.IsDefined(typeof(ReportTargetType), cmd.TargetType))
            throw new ValidationException("Некорректный тип жалобы.");

        if (!Enum.IsDefined(typeof(ReportReason), cmd.Reason))
            throw new ValidationException("Некорректная причина жалобы.");

        if (cmd.TargetId == Guid.Empty)
            throw new ValidationException("TargetId is required.");

        switch (cmd.TargetType)
        {
            case ReportTargetType.Photo:
                {
                    var photo = await _photos.GetByIdAsync(cmd.TargetId, ct);
                    if (photo is null || photo.IsDeleted)
                        throw new NotFoundException("Фото не найдено.");

                    break;
                }

            case ReportTargetType.Comment:
                {
                    var comment = await _comments.GetByIdAsync(cmd.TargetId, ct);
                    if (comment is null || comment.IsDeleted)
                        throw new NotFoundException("Комментарий не найден.");

                    break;
                }

            case ReportTargetType.User:
                {
                    var user = await _users.GetByIdAsync(cmd.TargetId, ct);
                    if (user is null || !user.IsActive)
                        throw new NotFoundException("Пользователь не найден.");

                    if (user.Id == _current.UserId)
                        throw new ValidationException("Нельзя отправить жалобу на самого себя.");

                    break;
                }

            default:
                throw new ValidationException("Некорректный тип жалобы.");
        }

        var exists = await _reports.ExistsSimilarPendingAsync(
            _current.UserId,
            cmd.TargetType,
            cmd.TargetId,
            ct);

        if (exists)
            throw new ValidationException("У вас уже есть активная жалоба на этот объект.");

        var report = Report.Create(
            _current.UserId,
            cmd.TargetType,
            cmd.TargetId,
            cmd.Reason,
            cmd.Description);

        await _reports.AddAsync(report, ct);
        await _uow.SaveChangesAsync(ct);

        return report.Id;
    }
}