using JeepneyFareCalculator.Domain.FareMatrices;

namespace JeepneyFareCalculator.Application.FareMatrices;

/// <summary>Where Fare Matrix Versions are kept.</summary>
public interface IFareMatrixStore
{
    /// <summary>
    /// Saves the fare matrix as a new Fare Matrix Version and makes it the Active
    /// Fare Matrix for its vehicle type, all in one transaction.
    /// </summary>
    Task<FareMatrixVersionSummary> AddAsActiveAsync(
        FareMatrix fareMatrix,
        string? note,
        DateTimeOffset uploadedAt,
        CancellationToken cancellationToken);

    /// <summary>The vehicle type's versions, newest first.</summary>
    Task<IReadOnlyList<FareMatrixVersionSummary>> ListVersionsAsync(
        VehicleType vehicleType,
        CancellationToken cancellationToken);
}
