using FluentAssertions;
using InMoment.Application.Abstractions.Security;
using InMoment.Infrastructure.Auth;
using InMoment.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InMoment.Infrastructure.Tests.DependencyInjection;

public sealed class ModerationServiceCollectionExtensionsTests
{
    [Fact]
    public void AddModerationServices_ShouldBindOptions_AndRegisterConfiguredSystemModeratorAccess()
    {
        var moderatorId = Guid.NewGuid();

        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ModerationOptions.SectionName}:SystemModeratorUserIds:0"] = moderatorId.ToString()
            })
            .Build();

        services.AddModerationServices(configuration);

        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<ModerationOptions>>().Value;
        var access = provider.GetRequiredService<ISystemModeratorAccess>();

        options.SystemModeratorUserIds.Should().ContainSingle().Which.Should().Be(moderatorId);
        access.Should().BeOfType<ConfiguredSystemModeratorAccess>();
        access.IsModerator(moderatorId).Should().BeTrue();
    }
}