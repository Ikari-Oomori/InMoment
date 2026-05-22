using FluentAssertions;
using InMoment.Domain.Common;
using InMoment.Infrastructure.Auth;
using Microsoft.Extensions.Options;

namespace InMoment.Infrastructure.Tests.Auth;

public sealed class ConfiguredSystemModeratorAccessTests
{
    [Fact]
    public void IsModerator_ShouldReturnFalse_WhenUserIdEmpty()
    {
        var service = Create(Guid.NewGuid());

        var result = service.IsModerator(Guid.Empty);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsModerator_ShouldReturnTrue_OnlyForConfiguredModerators()
    {
        var moderatorId = Guid.NewGuid();
        var service = Create(moderatorId);

        service.IsModerator(moderatorId).Should().BeTrue();
        service.IsModerator(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void Constructor_ShouldIgnoreEmptyModeratorIds()
    {
        var moderatorId = Guid.NewGuid();
        var options = Options.Create(new ModerationOptions
        {
            SystemModeratorUserIds = new List<Guid> { Guid.Empty, moderatorId }
        });

        var service = new ConfiguredSystemModeratorAccess(options);

        service.IsModerator(Guid.Empty).Should().BeFalse();
        service.IsModerator(moderatorId).Should().BeTrue();
    }

    [Fact]
    public void EnsureModerator_ShouldThrowForbidden_WhenUserUnauthorized()
    {
        var service = Create(Guid.NewGuid());

        var act = () => service.EnsureModerator(Guid.Empty);

        act.Should().Throw<ForbiddenException>()
            .WithMessage("Пользователь не авторизован.");
    }

    [Fact]
    public void EnsureModerator_ShouldThrowForbidden_WhenUserIsNotModerator()
    {
        var service = Create(Guid.NewGuid());

        var act = () => service.EnsureModerator(Guid.NewGuid());

        act.Should().Throw<ForbiddenException>()
            .WithMessage("Доступ разрешён только системному модератору.");
    }

    [Fact]
    public void EnsureModerator_ShouldNotThrow_WhenUserIsModerator()
    {
        var moderatorId = Guid.NewGuid();
        var service = Create(moderatorId);

        var act = () => service.EnsureModerator(moderatorId);

        act.Should().NotThrow();
    }

    private static ConfiguredSystemModeratorAccess Create(params Guid[] moderatorIds)
    {
        var options = Options.Create(new ModerationOptions
        {
            SystemModeratorUserIds = moderatorIds.ToList()
        });

        return new ConfiguredSystemModeratorAccess(options);
    }
}