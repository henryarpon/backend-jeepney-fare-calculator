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
/// What the distance estimator returned: an estimated distance in km,
/// "no route", or "provider unavailable".
/// </summary>
public sealed record DistanceEstimate
{
    private DistanceEstimate(DistanceEstimateOutcome outcome, decimal? estimatedDistanceKm)
    {
        Outcome = outcome;
        EstimatedDistanceKm = estimatedDistanceKm;
    }

    public DistanceEstimateOutcome Outcome { get; }

    /// <summary>The estimated distance in km. Only set when <see cref="Outcome"/> is <see cref="DistanceEstimateOutcome.Found"/>.</summary>
    public decimal? EstimatedDistanceKm { get; }

    public static DistanceEstimate Found(decimal estimatedDistanceKm)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(estimatedDistanceKm);
        return new DistanceEstimate(DistanceEstimateOutcome.Found, estimatedDistanceKm);
    }

    public static DistanceEstimate NoRoute { get; } =
        new(DistanceEstimateOutcome.NoRoute, null);

    public static DistanceEstimate ProviderUnavailable { get; } =
        new(DistanceEstimateOutcome.ProviderUnavailable, null);
}
