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

    /// <summary>
    /// Reads the CSV into a <see cref="FareMatrix"/>.
    /// </summary>
    /// <exception cref="InvalidFareMatrixFileException">
    /// The file is invalid. It lists every error, with rows numbered as in the file
    /// (the header is row 1).
    /// </exception>
    public static async Task<FareMatrix> ParseAsync(VehicleType vehicleType, Stream csv, CancellationToken cancellationToken)
    {
        // StreamReader skips a UTF-8 byte order mark, which spreadsheet apps often add.
        using var reader = new StreamReader(csv, leaveOpen: true);

        var header = await reader.ReadLineAsync(cancellationToken);
        if (header is null)
        {
            throw new InvalidFareMatrixFileException([new FareMatrixFileError(null, null, "The file is empty.")]);
        }

        // A wrong header means the columns can't be trusted, so the rows aren't read at all.
        if (header.Trim() != Header)
        {
            throw new InvalidFareMatrixFileException(
                [new FareMatrixFileError(1, null, $"The first row must be the header '{Header}'.")]);
        }

        var rows = new List<FareMatrixRow>();
        var fileRowOfEachRow = new List<int>();
        var errors = new List<FareMatrixFileError>();
        var fileRow = 1;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            fileRow++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var row = ReadRow(line, fileRow, errors);
            if (row is not null)
            {
                rows.Add(row);
                fileRowOfEachRow.Add(fileRow);
            }
        }

        // The Fare Matrix rules (distances 1..N, fares never going down, and so on) are
        // only checked once every row could be read: a row left out would make its
        // neighbours look like a gap. So a file is fixed in up to two rounds.
        if (errors.Count > 0)
        {
            throw new InvalidFareMatrixFileException(errors);
        }

        try
        {
            return new FareMatrix(vehicleType, rows);
        }
        catch (InvalidFareMatrixException invalid)
        {
            throw new InvalidFareMatrixFileException(invalid.Violations
                .Select(violation => new FareMatrixFileError(
                    violation.RowIndex is { } index ? fileRowOfEachRow[index] : null,
                    violation.Column?.ToString(),
                    violation.Message))
                .ToList());
        }
    }

    /// <summary>Reads one line, or adds its errors and returns <c>null</c>.</summary>
    private static FareMatrixRow? ReadRow(string line, int fileRow, List<FareMatrixFileError> errors)
    {
        var cells = line.Split(',');
        if (cells.Length != 3)
        {
            errors.Add(new FareMatrixFileError(
                fileRow,
                null,
                $"Every row must have 3 values ({Header}), but this one has {cells.Length}. " +
                "Use a dot as the decimal separator, not a comma."));
            return null;
        }

        var errorCountBefore = errors.Count;
        var distanceKm = ReadDistance(cells[0], fileRow, errors);
        var regularFare = ReadFare(cells[1], FareMatrixColumn.RegularFare, fileRow, errors);
        var discountedFare = ReadFare(cells[2], FareMatrixColumn.DiscountedFare, fileRow, errors);

        return errors.Count == errorCountBefore
            ? new FareMatrixRow(distanceKm, regularFare, discountedFare)
            : null;
    }

    private static int ReadDistance(string cell, int fileRow, List<FareMatrixFileError> errors)
    {
        const string column = nameof(FareMatrixColumn.DistanceKm);
        var text = cell.Trim();
        if (text.Length == 0)
        {
            errors.Add(new FareMatrixFileError(fileRow, column, $"{column} is missing."));
            return 0;
        }

        if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var distanceKm))
        {
            errors.Add(new FareMatrixFileError(fileRow, column, $"{column} must be a whole number, like 3."));
        }

        return distanceKm;
    }

    private static decimal ReadFare(string cell, FareMatrixColumn fareColumn, int fileRow, List<FareMatrixFileError> errors)
    {
        var column = fareColumn.ToString();
        var text = cell.Trim();
        if (text.Length == 0)
        {
            errors.Add(new FareMatrixFileError(fileRow, column, $"{column} is missing."));
            return 0;
        }

        if (!decimal.TryParse(
                text,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var fare))
        {
            errors.Add(new FareMatrixFileError(
                fileRow,
                column,
                $"{column} must be a number with a dot as the decimal separator, like 13.50."));
        }

        return fare;
    }
}
