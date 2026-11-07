namespace JeepneyFareCalculator.Application.Fares;

/// <summary>Why a trip can't be priced. Sent to the client by name, e.g. <c>"SamePoint"</c>.</summary>
public enum TripNotPricedReason
{
    /// <summary>The routing provider found no road route between the Origin and the Destination.</summary>
    NoRoute,

    /// <summary>The Origin and the Destination are the same point.</summary>
    SamePoint,

    /// <summary>The Origin or the Destination is outside the Philippines.</summary>
    OutsidePhilippines,

    /// <summary>The Charged Distance is longer than the Active Fare Matrix's last row.</summary>
    BeyondFareMatrix,

    /// <summary>The vehicle type has no Active Fare Matrix yet.</summary>
    NoActiveFareMatrix,
}

/// <summary>
/// Thrown when a valid request describes a trip that can't be priced (answered with 422).
/// The app never makes up a fare instead.
/// </summary>
/// <param name="message">What went wrong and what to try next, for the commuter.</param>
/// <param name="maxDistanceKm">The longest trip the matrix can price; only set for <see cref="TripNotPricedReason.BeyondFareMatrix"/>.</param>
public sealed class TripNotPricedException(TripNotPricedReason reason, string message, int? maxDistanceKm = null)
    : Exception(message)
{
    public TripNotPricedReason Reason { get; } = reason;

    public int? MaxDistanceKm { get; } = maxDistanceKm;
}
