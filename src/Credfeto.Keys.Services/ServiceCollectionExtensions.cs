using Microsoft.Extensions.DependencyInjection;

namespace Credfeto.Keys.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKeyServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<IChallengeService, ChallengeService>()
            .AddSingleton<IKeyManagementService, KeyManagementService>();
    }
}
