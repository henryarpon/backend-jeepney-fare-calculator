using JeepneyFareCalculator.Application.Distances;

namespace JeepneyFareCalculator.Api.Tests.Infrastructure;

/// <summary>
/// Stands in for the real routing provider. Set <see cref="NextEstimate"/> in a
/// test to choose what the next calculation sees: an estimated distance,
/// "no route" or "provider unavailable".
/// </summary>
public sealed class FakeDistanceEstimator : IDistanceEstimator
{
    public DistanceEstimate NextEstimate { get; set; } = DistanceEstimate.Found(1.0m);

    public Task<DistanceEstimate> EstimateAsync(
        Coordinates origin,
        Coordinates destination,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(NextEstimate);
    }
}
