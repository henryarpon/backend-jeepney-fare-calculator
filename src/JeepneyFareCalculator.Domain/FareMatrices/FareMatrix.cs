namespace JeepneyFareCalculator.Domain.FareMatrices;

/// <summary>
/// The official LTFRB table for one vehicle type: the Regular Fare and the
/// Discounted Fare for each distance in whole kilometers. It is the only source
/// of fare amounts (ADR 0001).
/// </summary>
public sealed class FareMatrix(VehicleType vehicleType, IReadOnlyList<FareMatrixRow> rows)
{
    public VehicleType VehicleType { get; } = vehicleType;

    /// <summary>The rows in the order they were given, one per distance.</summary>
    public IReadOnlyList<FareMatrixRow> Rows { get; } = rows;
}

/// <summary>One row of a <see cref="FareMatrix"/>: the fares per passenger for one distance.</summary>
public sealed record FareMatrixRow(int DistanceKm, decimal RegularFare, decimal DiscountedFare);
