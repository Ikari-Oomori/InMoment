using FluentAssertions;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Notifications.Devices;
using InMoment.Domain.Common;
using InMoment.Domain.Notifications;
using Moq;

namespace InMoment.Application.Tests.Notifications.Devices;

public sealed class RegisterDeviceTokenHandlerTests
{
    private readonly Mock<IDeviceTokenRepository> _tokens = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _current = new();

    private RegisterDeviceTokenHandler Create()
        => new(_tokens.Object, _uow.Object, _current.Object);

    [Fact]
    public async Task Handle_ShouldThrowForbidden_WhenUnauthorized()
    {
        _current.SetupGet(x => x.UserId).Returns(Guid.Empty);

        var handler = Create();

        var act = () => handler.Handle(
            new RegisterDeviceTokenCommand("token", PushPlatform.Android, PushProvider.Fcm, "Pixel"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Unauthorized.");
    }

    [Fact]
    public async Task Handle_ShouldCreateNewToken_WhenMissing()
    {
        var userId = Guid.NewGuid();

        _current.SetupGet(x => x.UserId).Returns(userId);
        _tokens.Setup(x => x.GetByTokenAsync("token_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeviceToken?)null);

        DeviceToken? added = null;
        _tokens.Setup(x => x.AddAsync(It.IsAny<DeviceToken>(), It.IsAny<CancellationToken>()))
            .Callback<DeviceToken, CancellationToken>((t, _) => added = t)
            .Returns(Task.CompletedTask);

        var handler = Create();

        var result = await handler.Handle(
            new RegisterDeviceTokenCommand("token_1", PushPlatform.Android, PushProvider.Fcm, "Pixel"),
            CancellationToken.None);

        result.Token.Should().Be("token_1");
        result.Platform.Should().Be(PushPlatform.Android);
        result.Provider.Should().Be(PushProvider.Fcm);

        added.Should().NotBeNull();
        added!.UserId.Should().Be(userId);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}