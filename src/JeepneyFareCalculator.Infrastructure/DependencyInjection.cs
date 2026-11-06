using JeepneyFareCalculator.Application.FareMatrices;
using JeepneyFareCalculator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JeepneyFareCalculator.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "DefaultConnection";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // The connection string is read when the DbContext is first needed, not here,
        // so tests (and any other host) can supply it through configuration.
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"Connection string '{ConnectionStringName}' is not configured. " +
                    "In development, set it with dotnet user-secrets (see back-end/CLAUDE.md).");

            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IFareMatrixStore, FareMatrixStore>();

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database");

        return services;
    }
}
