using JeepneyFareCalculator.Domain.FareMatrices;

namespace JeepneyFareCalculator.Application.FareMatrices;

/// <summary>
/// Reads an uploaded fare matrix CSV so the Administrator can check it before
/// confirming. Saves nothing.
/// </summary>
public sealed class PreviewFareMatrix
{
    public async Task<FareMatrixPreview> PreviewAsync(VehicleType vehicleType, Stream csv, CancellationToken cancellationToken)
    {
        var fareMatrix = await FareMatrixCsv.ParseAsync(vehicleType, csv, cancellationToken);

        return new FareMatrixPreview(
            fareMatrix.VehicleType,
            fareMatrix.Rows.Select(FareMatrixRowDto.From).ToList());
    }
}
