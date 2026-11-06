using System.Globalization;
using JeepneyFareCalculator.Domain.FareMatrices;

namespace JeepneyFareCalculator.Application.FareMatrices;

/// <summary>
/// Reads a fare matrix CSV: a header row <c>DistanceKm,RegularFare,DiscountedFare</c>,
/// then one comma-separated row per distance with a dot as the decimal separator.
/// </summary>
public static class FareMatrixCsv
{
    public const string Header = "DistanceKm,RegularFare,DiscountedFare";

    // TODO(#5): report every bad row as a validation error instead of throwing on the first one.
    public static async Task<FareMatrix> ParseAsync(VehicleType vehicleType, Stream csv, CancellationToken cancellationToken)
    {
        // StreamReader skips a UTF-8 byte order mark, which spreadsheet apps often add.
        using var reader = new StreamReader(csv, leaveOpen: true);

        var header = await reader.ReadLineAsync(cancellationToken);
        if (header?.Trim() != Header)
        {
            throw new FormatException($"The first row must be the header '{Header}'.");
        }

        var rows = new List<FareMatrixRow>();
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cells = line.Split(',');
            if (cells.Length != 3)
            {
                throw new FormatException($"Every row must have 3 values: '{line}'.");
            }

            rows.Add(new FareMatrixRow(
                DistanceKm: int.Parse(cells[0].Trim(), NumberStyles.None, CultureInfo.InvariantCulture),
                RegularFare: ParseFare(cells[1]),
                DiscountedFare: ParseFare(cells[2])));
        }

        return new FareMatrix(vehicleType, rows);
    }

    private static decimal ParseFare(string cell)
    {
        return decimal.Parse(cell.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
    }
}
