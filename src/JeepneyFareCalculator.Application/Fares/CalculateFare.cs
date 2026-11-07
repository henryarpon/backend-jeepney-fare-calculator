using FluentValidation;
using JeepneyFareCalculator.Application.Distances;
using JeepneyFareCalculator.Application.FareMatrices;
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
    public async Task<FareBreakdownDto> CalculateAsync(CalculateFareRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var party = new Party(request.Party!.Select(member => (member.PassengerType!.Value, member.Count!.Value)));

        // The matrix is read before the distance is estimated, so a vehicle type
        // that can't be priced never uses up routing quota.
        var activeFareMatrix = await fareMatrixStore.GetActiveAsync(request.VehicleType!.Value, cancellationToken)
            ?? throw new InvalidOperationException($"{request.VehicleType} has no Active Fare Matrix.");

        var estimate = await distanceEstimator.EstimateAsync(
            ToCoordinates(request.Origin!),
            ToCoordinates(request.Destination!),
            cancellationToken);
        if (estimate.EstimatedDistanceKm is not { } estimatedDistanceKm)
        {
            throw new InvalidOperationException($"The distance could not be estimated: {estimate.Outcome}.");
        }

        var breakdown = FareCalculation.Calculate(
            activeFareMatrix.FareMatrix,
            TripDistance.FromEstimate(estimatedDistanceKm),
            party);

        return FareBreakdownDto.From(breakdown, activeFareMatrix.Version);
    }

    private static Coordinates ToCoordinates(CoordinatesDto point) => new(point.Lat!.Value, point.Lng!.Value);
}
