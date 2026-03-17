using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sboss.Infrastructure.Repositories;

namespace Sboss.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSbossInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ISeasonRepository, InMemorySeasonRepository>();
        services.AddSingleton<ILevelSeedRepository, InMemoryLevelSeedRepository>();
        services.AddSingleton<IMatchResultRepository, InMemoryMatchResultRepository>();
        return services;
    }
}
