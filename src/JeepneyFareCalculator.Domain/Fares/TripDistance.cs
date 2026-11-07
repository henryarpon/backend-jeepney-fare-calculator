namespace JeepneyFareCalculator.Domain.Fares;

/// <summary>A trip's Estimated Distance and the Charged Distance that decides its fare matrix row.</summary>
public sealed record TripDistance
{
    private TripDistance(decimal estimatedKm, int chargedKm)
    {
        EstimatedKm = estimatedKm;
        ChargedKm = chargedKm;
    }

    /// <summary>The Estimated Distance in km, to 1 decimal place.</summary>
    public decimal EstimatedKm { get; }

    /// <summary>The Charged Distance: the Estimated Distance rounded up to the next whole km, at least 1.</summary>
    public int ChargedKm { get; }

    /// <summary>
    /// Rounds the routing provider's distance to 1 decimal place first and charges
    /// from that, so "5.0 km · charged as 5 km" always agrees with itself: 5.03 km
    /// is shown as 5.0 km and charged as 5 km, not 6.
    /// </summary>
    public static TripDistance FromEstimate(decimal estimatedKm)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(estimatedKm);

        var rounded = Math.Round(estimatedKm, 1, MidpointRounding.AwayFromZero);
        var charged = Math.Max(1, (int)Math.Ceiling(rounded));
        return new TripDistance(rounded, charged);
    }
}
