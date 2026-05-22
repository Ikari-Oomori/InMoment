using InMoment.Application.Abstractions.Security;
using InMoment.Infrastructure.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InMoment.Infrastructure.DependencyInjection;

public static class ModerationServiceCollectionExtensions
{
    public static IServiceCollection AddModerationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ModerationOptions>(
            configuration.GetSection(ModerationOptions.SectionName));

        services.AddSingleton<ISystemModeratorAccess, ConfiguredSystemModeratorAccess>();

        return services;
    }
}