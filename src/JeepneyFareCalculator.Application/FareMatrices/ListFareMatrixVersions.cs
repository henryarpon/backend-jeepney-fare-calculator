using JeepneyFareCalculator.Domain.FareMatrices;

namespace JeepneyFareCalculator.Application.FareMatrices;

/// <summary>Lists a vehicle type's Fare Matrix Versions, newest first.</summary>
public sealed class ListFareMatrixVersions(IFareMatrixStore store)
{
    public Task<IReadOnlyList<FareMatrixVersionSummary>> ListAsync(
        VehicleType vehicleType,
        CancellationToken cancellationToken)
    {
        return store.ListVersionsAsync(vehicleType, cancellationToken);
    }
}
