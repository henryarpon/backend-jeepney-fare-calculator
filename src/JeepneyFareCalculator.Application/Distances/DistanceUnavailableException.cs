namespace JeepneyFareCalculator.Application.Distances;

/// <summary>
/// Thrown when the routing provider is down, timed out or is out of quota, so the
/// Estimated Distance is unknown (answered with 503). There is deliberately no
/// fallback to a straight-line distance: it would understate fares without anyone noticing.
/// </summary>
public sealed class DistanceUnavailableException()
    : Exception("Couldn't estimate the distance right now, please try again.");
