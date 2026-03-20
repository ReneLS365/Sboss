using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Sboss.Infrastructure.Repositories;

namespace Sboss.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSbossInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:Default is required for infrastructure persistence.");
        }

        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddScoped<IAccountRepository, PostgresAccountRepository>();
        services.AddScoped<ISeasonRepository, PostgresSeasonRepository>();
        services.AddScoped<ILevelSeedRepository, PostgresLevelSeedRepository>();
        services.AddScoped<IMatchResultRepository, PostgresMatchResultRepository>();
        return services;
    }
}
