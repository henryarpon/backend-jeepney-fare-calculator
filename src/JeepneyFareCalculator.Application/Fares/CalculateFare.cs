using FluentValidation;
using JeepneyFareCalculator.Application.Distances;
using JeepneyFareCalculator.Application.FareMatrices;
using JeepneyFareCalculator.Domain.FareMatrices;
using JeepneyFareCalculator.Domain.Fares;

namespace JeepneyFareCalculator.Application.Fares;

/// <summary>
/// Prices a trip for a commuter: estimates the road distance, rounds it up to the
/// Charged Distance and reads the fares from the vehicle type's Active Fare Matrix.
/// </summary>
public sealed class CalculateFare(
    IValidator<CalculateFareRequest> validator,
    IFareMatrixStore fareMatrixStore,
    IDistanceEstimator distanceEstimator)
{
    /// <exception cref="ValidationException">The request is invalid (answered with 400).</exception>
    /// <exception cref="TripNotPricedException">The trip can't be priced, and why (answered with 422).</exception>
    /// <exception cref="DistanceUnavailableException">The routing provider is unavailable (answered with 503).</exception>
    public async Task<FareBreakdownDto> CalculateAsync(CalculateFareRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var party = new Party(request.Party!.Select(member => (member.PassengerType!.Value, member.Count!.Value)));

        // Every check that needs no routing provider runs first, so a trip that
        // can't be priced never uses up routing quota.
        var origin = ToCoordinates(request.Origin!);
        var destination = ToCoordinates(request.Destination!);
        if (!PhilippinesBoundingBox.Contains(origin) || !PhilippinesBoundingBox.Contains(destination))
        {
            throw new TripNotPricedException(
                TripNotPricedReason.OutsidePhilippines,
                "The Origin or Destination is outside the Philippines. Choose places in the Philippines.");
        }

        // The routing provider would answer a zero-length route, which would be
        // priced as the shortest trip. The spec rejects it instead.
        if (origin == destination)
        {
            throw new TripNotPricedException(
                TripNotPricedReason.SamePoint,
                "The Origin and Destination are the same place. Choose a different Destination.");
        }

        var activeFareMatrix = await fareMatrixStore.GetActiveAsync(request.VehicleType!.Value, cancellationToken)
            ?? throw new TripNotPricedException(
                TripNotPricedReason.NoActiveFareMatrix,
                $"There are no {VehicleTypeName(request.VehicleType.Value)} fares yet. " +
                "Try the other vehicle type, or check back later.");

        var estimate = await distanceEstimator.EstimateAsync(origin, destination, cancellationToken);
        var estimatedDistanceKm = estimate.Outcome switch
        {
            DistanceEstimateOutcome.Found => estimate.EstimatedDistanceKm!.Value,
            DistanceEstimateOutcome.NoRoute => throw new TripNotPricedException(
                TripNotPricedReason.NoRoute,
                "No road route was found between the Origin and Destination. Try a place on or near a road."),
            DistanceEstimateOutcome.ProviderUnavailable => throw new DistanceUnavailableException(),
            _ => throw new InvalidOperationException($"Unknown distance estimate outcome: {estimate.Outcome}."),
        };

        try
        {
            var breakdown = FareCalculation.Calculate(
                activeFareMatrix.FareMatrix,
                TripDistance.FromEstimate(estimatedDistanceKm),
                party);

            return FareBreakdownDto.From(breakdown, activeFareMatrix.Version);
        }
        catch (ChargedDistanceBeyondFareMatrixException beyond)
        {
            throw new TripNotPricedException(
                TripNotPricedReason.BeyondFareMatrix,
                $"This trip is charged as {beyond.ChargedKm} km, but the fare matrix only goes up to {beyond.MaxKm} km. " +
                "Try pricing it as shorter trips, one ride at a time.",
                beyond.MaxKm);
        }
    }

    private static Coordinates ToCoordinates(CoordinatesDto point) => new(point.Lat!.Value, point.Lng!.Value);

    /// <summary>The vehicle type as the commuter sees it on screen.</summary>
    private static string VehicleTypeName(VehicleType vehicleType) => vehicleType switch
    {
        VehicleType.TraditionalJeepney => "Traditional Jeepney",
        VehicleType.ModernJeepney => "Modern Jeepney",
        _ => throw new ArgumentOutOfRangeException(nameof(vehicleType), vehicleType, null),
    };
}
