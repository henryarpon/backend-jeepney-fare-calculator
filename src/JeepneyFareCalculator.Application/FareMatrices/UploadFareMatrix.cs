using JeepneyFareCalculator.Domain.FareMatrices;

namespace JeepneyFareCalculator.Application.FareMatrices;

/// <summary>
/// Saves an uploaded fare matrix CSV as a new Fare Matrix Version, which
/// becomes the Active Fare Matrix for its vehicle type immediately.
/// </summary>
public sealed class UploadFareMatrix(IFareMatrixStore store, TimeProvider timeProvider)
{
    /// <summary>The longest note the Administrator can attach, e.g. an LTFRB order number.</summary>
    public const int NoteMaxLength = 200;

    public async Task<FareMatrixVersionSummary> UploadAsync(
        VehicleType vehicleType,
        Stream csv,
        string? note,
        CancellationToken cancellationToken)
    {
        var fareMatrix = await FareMatrixCsv.ParseAsync(vehicleType, csv, cancellationToken);

        return await store.AddAsActiveAsync(
            fareMatrix,
            string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            timeProvider.GetUtcNow(),
            cancellationToken);
    }
}
