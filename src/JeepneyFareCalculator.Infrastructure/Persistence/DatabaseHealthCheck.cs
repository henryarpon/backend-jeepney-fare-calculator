using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JeepneyFareCalculator.Infrastructure.Persistence;

/// <summary>Reports healthy when the application can reach its database.</summary>
public sealed class DatabaseHealthCheck(AppDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

        return canConnect
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Can't connect to the database.");
    }
}
