using JeepneyFareCalculator.Application.Distances;

namespace JeepneyFareCalculator.Api.Tests.Infrastructure;

/// <summary>
/// Stands in for the real routing provider. Set <see cref="NextEstimate"/> in a
/// test to choose what the next calculation sees: an estimated distance,
/// "no route" or "provider unavailable".
/// </summary>
public sealed class FakeDistanceEstimator : IDistanceEstimator
{
    private int _callCount;

    public DistanceEstimate NextEstimate { get; set; } = DistanceEstimate.Found(1.0m);

    /// <summary>
    /// How many times the routing provider was asked, i.e. how much quota a real
    /// provider would have used. Only meaningful behind the cache (<see cref="RoutingQuotaApiFactory"/>).
    /// </summary>
    public int CallCount => Volatile.Read(ref _callCount);

    public Task<DistanceEstimate> EstimateAsync(
        Coordinates origin,
        Coordinates destination,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);
        return Task.FromResult(NextEstimate);
    }
}
