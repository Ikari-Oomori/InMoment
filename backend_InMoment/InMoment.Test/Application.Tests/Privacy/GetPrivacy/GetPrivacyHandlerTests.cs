using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Privacy.Common;
using InMoment.Application.Features.Privacy.GetPrivacy;
using InMoment.Domain.Common;
using InMoment.Domain.Privacy;
using Moq;

namespace InMoment.Application.Tests.Privacy.GetPrivacy;

public sealed class GetPrivacyHandlerTests
{
    private readonly Mock<IPrivacySettingsRepository> _privacy = new();
    private readonly Mock<ICurrentUser> _current = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private GetPrivacyHandler Create()
        => new(_privacy.Object, _current.Object, _uow.Object);

    [Fact]
    public async Task Handle_ShouldThrowForbiddenException_WhenUnauthorized()
    {
        _current.Setup(x => x.UserId).Returns(Guid.Empty);

        var handler = Create();

        var act = () => handler.Handle(new GetPrivacyQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Пользователь не авторизован.");
    }

    [Fact]
    public async Task Handle_ShouldReturnExistingSettings_WhenTheyExist()
    {
        var userId = Guid.NewGuid();
        var settings = PrivacySettings.CreateDefault(userId);
        settings.Update(PrivacyAudience.FriendsOnly, PrivacyAudience.Nobody, false, true);

        _current.Setup(x => x.UserId).Returns(userId);
        _privacy.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        var handler = Create();

        var result = await handler.Handle(new GetPrivacyQuery(), CancellationToken.None);

        result.Should().BeEquivalentTo(new PrivacySettingsDto(
            PrivacyAudience.FriendsOnly,
            PrivacyAudience.Nobody,
            false,
            true));

        _privacy.Verify(x => x.AddAsync(It.IsAny<PrivacySettings>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldCreateDefaultSettings_WhenMissing()
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

        var result = await handler.Handle(new GetPrivacyQuery(), CancellationToken.None);

        added.Should().NotBeNull();
        added!.UserId.Should().Be(userId);

        result.Should().BeEquivalentTo(new PrivacySettingsDto(
            PrivacyAudience.Everyone,
            PrivacyAudience.Everyone,
            true,
            true));

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}