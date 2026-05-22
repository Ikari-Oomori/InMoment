using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Reports.CreateReport;
using InMoment.Domain.Common;
using InMoment.Domain.Media;
using InMoment.Domain.Reports;
using InMoment.Domain.Users;
using Moq;

namespace InMoment.Application.Tests.Reports.CreateReport;

public sealed class CreateReportHandlerTests
{
    private readonly Mock<IReportRepository> _reports = new();
    private readonly Mock<IPhotoRepository> _photos = new();
    private readonly Mock<ICommentRepository> _comments = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private CreateReportHandler Create()
        => new(_reports.Object, _photos.Object, _comments.Object, _users.Object, _current.Object, _uow.Object);

    [Fact]
    public async Task Handle_ShouldThrow_WhenUnauthorized()
    {
        _current.Setup(x => x.UserId).Returns(Guid.Empty);

        var handler = Create();

        var act = () => handler.Handle(
            new CreateReportCommand(ReportTargetType.Photo, Guid.NewGuid(), ReportReason.Spam, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Пользователь не авторизован.");
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTargetTypeInvalid()
    {
        _current.Setup(x => x.UserId).Returns(Guid.NewGuid());

        var handler = Create();

        var act = () => handler.Handle(
            new CreateReportCommand((ReportTargetType)999, Guid.NewGuid(), ReportReason.Spam, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Некорректный тип жалобы.");
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenReasonInvalid()
    {
        _current.Setup(x => x.UserId).Returns(Guid.NewGuid());

        var handler = Create();

        var act = () => handler.Handle(
            new CreateReportCommand(ReportTargetType.Photo, Guid.NewGuid(), (ReportReason)999, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Некорректная причина жалобы.");
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTargetIdEmpty()
    {
        _current.Setup(x => x.UserId).Returns(Guid.NewGuid());

        var handler = Create();

        var act = () => handler.Handle(
            new CreateReportCommand(ReportTargetType.Photo, Guid.Empty, ReportReason.Spam, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("TargetId is required.");
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenPhotoTargetNotFound()
    {
        _current.Setup(x => x.UserId).Returns(Guid.NewGuid());
        _photos.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Photo?)null);

        var handler = Create();

        var act = () => handler.Handle(
            new CreateReportCommand(ReportTargetType.Photo, Guid.NewGuid(), ReportReason.Spam, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Фото не найдено.");
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenPhotoDeleted()
    {
        _current.Setup(x => x.UserId).Returns(Guid.NewGuid());

        var photo = Photo.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "key",
            "image/jpeg",
            100);

        photo.MarkDeleted(photo.UploadedByUserId, photo.UploadedByUserId);

        _photos.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);

        var handler = Create();

        var act = () => handler.Handle(
            new CreateReportCommand(ReportTargetType.Photo, Guid.NewGuid(), ReportReason.Spam, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Фото не найдено.");
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCommentDeleted()
    {
        var currentUserId = Guid.NewGuid();
        _current.Setup(x => x.UserId).Returns(currentUserId);

        var comment = Comment.CreateRoot(Guid.NewGuid(), Guid.NewGuid(), "hello");
        comment.Delete(comment.UserId);

        _comments.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);

        var handler = Create();

        var act = () => handler.Handle(
            new CreateReportCommand(ReportTargetType.Comment, Guid.NewGuid(), ReportReason.Harassment, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Комментарий не найден.");
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenUserInactive()
    {
        _current.Setup(x => x.UserId).Returns(Guid.NewGuid());

        var user = CreateUser(Guid.NewGuid(), "inactive_user");
        user.Deactivate();

        _users.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = Create();

        var act = () => handler.Handle(
            new CreateReportCommand(ReportTargetType.User, Guid.NewGuid(), ReportReason.Other, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Пользователь не найден.");
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenReportingSelf()
    {
        var currentUserId = Guid.NewGuid();
        _current.Setup(x => x.UserId).Returns(currentUserId);

        var user = CreateUser(currentUserId, "self_user");

        _users.Setup(x => x.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = Create();

        var act = () => handler.Handle(
            new CreateReportCommand(ReportTargetType.User, currentUserId, ReportReason.Other, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Нельзя отправить жалобу на самого себя.");
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenDuplicatePending()
    {
        var currentUserId = Guid.NewGuid();
        _current.Setup(x => x.UserId).Returns(currentUserId);

        var photo = Photo.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "key",
            "image/jpeg",
            100);

        _photos.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);

        _reports.Setup(x => x.ExistsSimilarPendingAsync(
                currentUserId,
                ReportTargetType.Photo,
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = Create();

        var act = () => handler.Handle(
            new CreateReportCommand(ReportTargetType.Photo, Guid.NewGuid(), ReportReason.Spam, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("У вас уже есть активная жалоба на этот объект.");
    }

    [Fact]
    public async Task Handle_ShouldCreateReport()
    {
        var userId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        _current.Setup(x => x.UserId).Returns(userId);

        var photo = Photo.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "key",
            "image/jpeg",
            100);

        _photos.Setup(x => x.GetByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);

        _reports.Setup(x => x.ExistsSimilarPendingAsync(
                userId,
                ReportTargetType.Photo,
                targetId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Report? added = null;

        _reports.Setup(x => x.AddAsync(It.IsAny<Report>(), It.IsAny<CancellationToken>()))
            .Callback<Report, CancellationToken>((r, _) => added = r)
            .Returns(Task.CompletedTask);

        var handler = Create();

        var id = await handler.Handle(
            new CreateReportCommand(ReportTargetType.Photo, targetId, ReportReason.Spam, "test"),
            CancellationToken.None);

        added.Should().NotBeNull();
        added!.ReporterUserId.Should().Be(userId);
        added.TargetType.Should().Be(ReportTargetType.Photo);
        added.TargetId.Should().Be(targetId);
        added.Reason.Should().Be(ReportReason.Spam);
        added.Description.Should().Be("test");

        id.Should().Be(added.Id);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static User CreateUser(Guid id, string userName)
    {
        var user = User.Create(
            email: $"{userName}@test.com",
            passwordHash: "hash",
            userName: userName,
            firstName: "Test",
            lastName: "User");

        typeof(User)
            .GetProperty(nameof(User.Id))!
            .SetValue(user, id);

        return user;
    }
}