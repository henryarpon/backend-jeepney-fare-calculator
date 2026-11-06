using JeepneyFareCalculator.Domain.FareMatrices;

namespace JeepneyFareCalculator.Application.FareMatrices;

/// <summary>One row of a fare matrix, as the API returns it.</summary>
public sealed record FareMatrixRowDto(int DistanceKm, decimal RegularFare, decimal DiscountedFare)
{
    public static FareMatrixRowDto From(FareMatrixRow row) => new(row.DistanceKm, row.RegularFare, row.DiscountedFare);
}

/// <summary>Body of <c>POST /api/admin/fare-matrices/preview</c>: the parsed rows. Nothing is saved.</summary>
public sealed record FareMatrixPreview(VehicleType VehicleType, IReadOnlyList<FareMatrixRowDto> Rows);

/// <summary>
/// One Fare Matrix Version without its rows: an item of <c>GET /api/admin/fare-matrices</c>,
/// and the body of a successful upload.
/// </summary>
public sealed record FareMatrixVersionSummary(
    int Id,
    VehicleType VehicleType,
    DateTimeOffset UploadedAt,
    string? Note,
    bool IsActive,
    int RowCount);
