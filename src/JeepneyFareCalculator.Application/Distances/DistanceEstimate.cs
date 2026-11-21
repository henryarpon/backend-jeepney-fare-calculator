namespace JeepneyFareCalculator.Application.Distances;

public enum DistanceEstimateOutcome
{
    /// <summary>A road route was found; see <see cref="DistanceEstimate.EstimatedDistanceKm"/>.</summary>
    Found,

    /// <summary>The provider found no road route between the two points.</summary>
    NoRoute,

    /// <summary>The provider is down, timed out, or is out of quota.</summary>
    ProviderUnavailable,
}

/// <summary>
/// What the distance estimator returned: an estimated distance in km with the
/// route line it was measured along, "no route", or "provider unavailable".
/// </summary>
public sealed record DistanceEstimate
{
    private DistanceEstimate(
        DistanceEstimateOutcome outcome,
        decimal? estimatedDistanceKm,
        IReadOnlyList<Coordinates> routeLine)
    {
        Outcome = outcome;
        EstimatedDistanceKm = estimatedDistanceKm;
        RouteLine = routeLine;
    }

    public DistanceEstimateOutcome Outcome { get; }

    /// <summary>The estimated distance in km. Only set when <see cref="Outcome"/> is <see cref="DistanceEstimateOutcome.Found"/>.</summary>
    public decimal? EstimatedDistanceKm { get; }

    /// <summary>
    /// The road path the Estimated Distance was measured along, from the Origin to the Destination,
    /// so the commuter can see it on the map. Empty unless <see cref="Outcome"/> is
    /// <see cref="DistanceEstimateOutcome.Found"/>.
    /// </summary>
    public IReadOnlyList<Coordinates> RouteLine { get; }

    /// <param name="estimatedDistanceKm">The road distance in km.</param>
    /// <param name="routeLine">The road path it was measured along. Left out, the route line is empty.</param>
    public static DistanceEstimate Found(decimal estimatedDistanceKm, IReadOnlyList<Coordinates>? routeLine = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(estimatedDistanceKm);
        return new DistanceEstimate(DistanceEstimateOutcome.Found, estimatedDistanceKm, routeLine ?? []);
    }

    public static DistanceEstimate NoRoute { get; } =
        new(DistanceEstimateOutcome.NoRoute, null, []);

    public static DistanceEstimate ProviderUnavailable { get; } =
        new(DistanceEstimateOutcome.ProviderUnavailable, null, []);
}
