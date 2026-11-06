namespace JeepneyFareCalculator.Application.Distances;

/// <summary>
/// Estimates the road distance between a trip's origin and destination.
/// This is the application's only seam to the outside world (the routing provider).
/// </summary>
public interface IDistanceEstimator
{
    Task<DistanceEstimate> EstimateAsync(
        Coordinates origin,
        Coordinates destination,
        CancellationToken cancellationToken);
}
