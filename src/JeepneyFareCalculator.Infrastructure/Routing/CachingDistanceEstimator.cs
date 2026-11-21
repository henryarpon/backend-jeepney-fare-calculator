using System.Globalization;
using JeepneyFareCalculator.Application.Distances;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace JeepneyFareCalculator.Infrastructure.Routing;

/// <summary>
/// Remembers Estimated Distances in memory so a repeated trip doesn't use up the
/// routing provider's free quota, for example when the commuter only changes the party.
/// </summary>
public sealed class CachingDistanceEstimator(
    [FromKeyedServices(CachingDistanceEstimator.RoutingProviderKey)] IDistanceEstimator routingProvider,
    IMemoryCache cache)
    : IDistanceEstimator
{
    /// <summary>The service key of the real routing provider this cache sits in front of.</summary>
    public const string RoutingProviderKey = "RoutingProvider";

    // Roads rarely change within a day, and the cache empties when the API restarts.
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    public async Task<DistanceEstimate> EstimateAsync(
        Coordinates origin,
        Coordinates destination,
        CancellationToken cancellationToken)
    {
        var key = CacheKey(origin, destination);
        if (cache.TryGetValue(key, out DistanceEstimate? cached) && cached is not null)
        {
            return cached;
        }

        var estimate = await routingProvider.EstimateAsync(origin, destination, cancellationToken);

        // Only a found route is kept (with its route line). "No route" and "provider
        // unavailable" may be temporary, so the next request asks the provider again.
        if (estimate.Outcome == DistanceEstimateOutcome.Found)
        {
            cache.Set(key, estimate, Lifetime);
        }

        return estimate;
    }

    /// <summary>
    /// The Origin and Destination rounded to 4 decimal places (about 11 m), so pins
    /// dropped a step apart share one estimate. The vehicle type and party never reach
    /// the distance estimator, so they can't split the cache.
    /// </summary>
    private static string CacheKey(Coordinates origin, Coordinates destination)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"distance:{Round(origin.Latitude)},{Round(origin.Longitude)}->{Round(destination.Latitude)},{Round(destination.Longitude)}");
    }

    private static double Round(double degrees) => Math.Round(degrees, 4);
}
