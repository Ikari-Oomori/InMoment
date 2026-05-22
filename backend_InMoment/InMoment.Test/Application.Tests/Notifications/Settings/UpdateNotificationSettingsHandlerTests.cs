using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Notifications.Settings;
using InMoment.Domain.Common;
using InMoment.Domain.Notifications;
using Moq;

namespace InMoment.Application.Tests.Notifications.Settings;

public sealed class UpdateNotificationSettingsHandlerTests
{
    private readonly Mock<INotificationSettingsRepository> _settings = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _current = new();

    private UpdateNotificationSettingsHandler Create()
        => new(_settings.Object, _uow.Object, _current.Object);

    [Fact]
    public async Task Handle_ShouldThrowForbidden_WhenUnauthorized()
    {
        _current.SetupGet(x => x.UserId).Returns(Guid.Empty);

        var handler = Create();

        var act = () => handler.Handle(
            new UpdateNotificationSettingsCommand(
                true, true, true, true, true, true, true, true, true),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Unauthorized.");
    }

    [Fact]
    public async Task Handle_ShouldCreateDefaultAndUpdate_WhenSettingsMissing()
    {
        var userId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(userId);
        _settings.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationSettings?)null);

        NotificationSettings? added = null;
        _settings.Setup(x => x.AddAsync(It.IsAny<NotificationSettings>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationSettings, CancellationToken>((s, _) => added = s)
            .Returns(Task.CompletedTask);

        var handler = Create();

        var result = await handler.Handle(
            new UpdateNotificationSettingsCommand(
                true,
                false,
                true,
                false,
                true,
                false,
                true,
                false,
                true),
            CancellationToken.None);

        result.PushEnabled.Should().BeTrue();
        result.PushGroupInvitations.Should().BeFalse();
        result.PushReactions.Should().BeTrue();
        result.PushComments.Should().BeFalse();
        result.PushReplies.Should().BeTrue();
        result.PushMentions.Should().BeFalse();
        result.PushPosts.Should().BeTrue();
        result.PushRetention.Should().BeFalse();
        result.PushProductUpdates.Should().BeTrue();

        added.Should().NotBeNull();
        added!.UserId.Should().Be(userId);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}