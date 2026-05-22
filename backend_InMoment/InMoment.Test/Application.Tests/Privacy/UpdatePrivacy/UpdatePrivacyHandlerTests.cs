using FluentAssertions;
using FluentValidation;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Privacy.UpdatePrivacy;
using InMoment.Domain.Common;
using InMoment.Domain.Privacy;
using Moq;

namespace InMoment.Application.Tests.Privacy.UpdatePrivacy;

public sealed class UpdatePrivacyHandlerTests
{
    private readonly Mock<IPrivacySettingsRepository> _privacy = new();
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private UpdatePrivacyHandler Create()
        => new(_privacy.Object, _current.Object, _uow.Object, new UpdatePrivacyValidator());

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUnauthorized()
    {
        _current.Setup(x => x.UserId).Returns(Guid.Empty);

        var handler = Create();

        var act = () => handler.Handle(
            new UpdatePrivacyCommand(
                PrivacyAudience.Everyone,
                PrivacyAudience.Everyone,
                true,
                true),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Пользователь не авторизован.");
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenEnumInvalid()
    {
        var userId = Guid.NewGuid();
        _current.Setup(x => x.UserId).Returns(userId);

        var handler = Create();

        var act = () => handler.Handle(
            new UpdatePrivacyCommand(
                (PrivacyAudience)999,
                PrivacyAudience.Everyone,
                true,
                true),
            CancellationToken.None);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task Handle_ShouldUpdateExistingSettings()
    {
        var userId = Guid.NewGuid();
        var settings = PrivacySettings.CreateDefault(userId);

        _current.Setup(x => x.UserId).Returns(userId);
        _privacy.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var handler = Create();

        await handler.Handle(
            new UpdatePrivacyCommand(
                PrivacyAudience.FriendsOnly,
                PrivacyAudience.Nobody,
                false,
                false),
            CancellationToken.None);

        settings.AllowFriendRequestsFrom.Should().Be(PrivacyAudience.FriendsOnly);
        settings.AllowGroupInvitesFrom.Should().Be(PrivacyAudience.Nobody);
        settings.DiscoverableByContacts.Should().BeFalse();
        settings.DiscoverableBySearch.Should().BeFalse();

        _privacy.Verify(x => x.AddAsync(It.IsAny<PrivacySettings>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldCreateDefaultSettings_WhenMissing_AndThenUpdate()
    {
        var userId = Guid.NewGuid();
        PrivacySettings? added = null;

        _current.Setup(x => x.UserId).Returns(userId);
        _privacy.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivacySettings?)null);

        _privacy.Setup(x => x.AddAsync(It.IsAny<PrivacySettings>(), It.IsAny<CancellationToken>()))
            .Callback<PrivacySettings, CancellationToken>((s, _) => added = s)
            .Returns(Task.CompletedTask);

        var handler = Create();

        await handler.Handle(
            new UpdatePrivacyCommand(
                PrivacyAudience.Nobody,
                PrivacyAudience.FriendsOnly,
                false,
                true),
            CancellationToken.None);

        added.Should().NotBeNull();
        added!.UserId.Should().Be(userId);
        added.AllowFriendRequestsFrom.Should().Be(PrivacyAudience.Nobody);
        added.AllowGroupInvitesFrom.Should().Be(PrivacyAudience.FriendsOnly);
        added.DiscoverableByContacts.Should().BeFalse();
        added.DiscoverableBySearch.Should().BeTrue();

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}