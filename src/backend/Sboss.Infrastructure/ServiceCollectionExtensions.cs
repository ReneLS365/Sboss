using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sboss.Infrastructure.Repositories;
using Sboss.Infrastructure.Services;

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

        services.AddSingleton(_ => NpgsqlDataSourceRegistry.Create(connectionString));
        services.AddScoped<IAccountRepository, PostgresAccountRepository>();
        services.AddScoped<ISeasonRepository, PostgresSeasonRepository>();
        services.AddScoped<ILevelSeedRepository, PostgresLevelSeedRepository>();
        services.AddScoped<IMatchResultRepository, PostgresMatchResultRepository>();
        services.AddScoped<IContractJobRepository, PostgresContractJobRepository>();
        services.AddScoped<IContractJobApplicationRepository, PostgresContractJobApplicationRepository>();
        services.AddScoped<IEconomyTransactionService, EconomyTransactionService>();
        services.AddScoped<IContractJobTransitionService, ContractJobTransitionService>();
        services.AddScoped<IContractJobApplicationService, ContractJobApplicationService>();
        return services;
    }
}
