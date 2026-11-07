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

    /// <summary>
    /// Makes the version the Active Fare Matrix for its vehicle type and deactivates
    /// that vehicle type's other active version, all in one transaction. A version
    /// that is already active is left as it is.
    /// </summary>
    /// <returns>The version, now active, or <c>null</c> when no version has this id.</returns>
    Task<FareMatrixVersionSummary?> ActivateAsync(int id, CancellationToken cancellationToken);

    /// <summary>One version with its rows in distance order, or <c>null</c> when no version has this id.</summary>
    Task<FareMatrixVersionDetails?> GetVersionAsync(int id, CancellationToken cancellationToken);

    /// <summary>The vehicle type's Active Fare Matrix, or <c>null</c> when it has none yet.</summary>
    Task<ActiveFareMatrix?> GetActiveAsync(VehicleType vehicleType, CancellationToken cancellationToken);
}
