using System.Globalization;

namespace JeepneyFareCalculator.Domain.FareMatrices;

/// <summary>
/// The official LTFRB table for one vehicle type: the Regular Fare and the
/// Discounted Fare for each distance in whole kilometers. It is the only source
/// of fare amounts (ADR 0001).
/// </summary>
/// <remarks>
/// A wrong matrix would misprice every trip, so an invalid one can't be created:
/// the constructor throws <see cref="InvalidFareMatrixException"/> listing every broken rule.
/// </remarks>
public sealed class FareMatrix
{
    public FareMatrix(VehicleType vehicleType, IReadOnlyList<FareMatrixRow> rows)
    {
        var violations = FindViolations(rows);
        if (violations.Count > 0)
        {
            throw new InvalidFareMatrixException(violations);
        }

        VehicleType = vehicleType;
        Rows = rows.ToList();
    }

    public VehicleType VehicleType { get; }

    /// <summary>The rows in the order they were given, one per distance.</summary>
    public IReadOnlyList<FareMatrixRow> Rows { get; }

    private const string DistancesRule = "Distances run 1, 2, 3 and so on, with no gaps or duplicates.";

    private static List<FareMatrixRuleViolation> FindViolations(IReadOnlyList<FareMatrixRow> rows)
    {
        var violations = new List<FareMatrixRuleViolation>();
        if (rows.Count == 0)
        {
            violations.Add(new(null, null, "The fare matrix has no rows. Add one row per distance, starting at 1 km."));
        }

        // Each row is checked on its own and against the row above it, so a single
        // gap or duplicate is reported once, not again for every row after it.
        // A negative value is reported only as negative, not also as out of order.
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var above = index > 0 ? rows[index - 1] : null;

            AddIfNegative(row.DistanceKm, FareMatrixColumn.DistanceKm);
            AddIfNegative(row.RegularFare, FareMatrixColumn.RegularFare);
            AddIfNegative(row.DiscountedFare, FareMatrixColumn.DiscountedFare);

            if (row.DistanceKm >= 0 && above is not { DistanceKm: < 0 })
            {
                var expectedKm = above is null ? 1 : above.DistanceKm + 1;
                if (row.DistanceKm == above?.DistanceKm)
                {
                    violations.Add(new(index, FareMatrixColumn.DistanceKm,
                        $"Distance {row.DistanceKm} km appears twice. {DistancesRule}"));
                }
                else if (row.DistanceKm != expectedKm)
                {
                    violations.Add(new(index, FareMatrixColumn.DistanceKm,
                        $"DistanceKm must be {expectedKm} here. {DistancesRule}"));
                }
            }

            if (row.RegularFare >= 0 && row.DiscountedFare > row.RegularFare)
            {
                violations.Add(new(index, FareMatrixColumn.DiscountedFare,
                    $"DiscountedFare is higher than RegularFare ({Money(row.RegularFare)}) in the same row."));
            }

            if (above is not null)
            {
                AddIfLowerThanAbove(row.RegularFare, above.RegularFare, FareMatrixColumn.RegularFare);
                AddIfLowerThanAbove(row.DiscountedFare, above.DiscountedFare, FareMatrixColumn.DiscountedFare);
            }

            void AddIfLowerThanAbove(decimal fare, decimal fareAbove, FareMatrixColumn column)
            {
                if (fare >= 0 && fare < fareAbove)
                {
                    violations.Add(new(index, column,
                        $"{column} is lower than in the row above ({Money(fareAbove)}). Fares never go down as distance goes up."));
                }
            }

            void AddIfNegative(decimal value, FareMatrixColumn column)
            {
                if (value < 0)
                {
                    violations.Add(new(index, column, $"{column} must not be negative."));
                }
            }
        }

        // Listed the way the file reads: row by row, and left to right within a row.
        return violations
            .OrderBy(violation => violation.RowIndex)
            .ThenBy(violation => violation.Column)
            .ToList();
    }

    /// <summary>A fare as the CSV writes it, e.g. 13.50, whatever the server's culture.</summary>
    private static string Money(decimal fare) => fare.ToString("0.00", CultureInfo.InvariantCulture);
}

/// <summary>One row of a <see cref="FareMatrix"/>: the fares per passenger for one distance.</summary>
public sealed record FareMatrixRow(int DistanceKm, decimal RegularFare, decimal DiscountedFare);
