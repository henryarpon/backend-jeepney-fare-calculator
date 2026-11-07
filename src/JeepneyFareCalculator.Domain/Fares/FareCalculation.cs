using JeepneyFareCalculator.Domain.FareMatrices;

namespace JeepneyFareCalculator.Domain.Fares;

/// <summary>
/// Prices a trip from the Active Fare Matrix. Every amount is read straight from
/// the matrix row for the Charged Distance, with no extra rounding (ADR 0001).
/// </summary>
public static class FareCalculation
{
    /// <exception cref="ChargedDistanceBeyondFareMatrixException">
    /// The Charged Distance is longer than the matrix's last row. Fares are never extrapolated.
    /// </exception>
    public static FareBreakdown Calculate(FareMatrix fareMatrix, TripDistance distance, Party party)
    {
        var rowIndex = distance.ChargedKm - 1;
        if (rowIndex >= fareMatrix.Rows.Count)
        {
            throw new ChargedDistanceBeyondFareMatrixException(distance.ChargedKm, fareMatrix.Rows.Count);
        }

        // A valid matrix runs 1, 2, 3 km and so on with no gaps, so the row for
        // n km is at index n - 1.
        var row = fareMatrix.Rows[rowIndex];

        var lines = party.Members
            .Select(member => Line(row, member.PassengerType, member.Count))
            .ToList();

        return new FareBreakdown(
            distance,
            lines,
            lines.Sum(line => line.Subtotal),
            Excerpt(fareMatrix, rowIndex));
    }

    /// <summary>
    /// Regular passengers pay the Regular Fare; every other type pays the Discounted
    /// Fare from the same row. There is one discounted column, so discounts never stack.
    /// </summary>
    private static FareLine Line(FareMatrixRow row, PassengerType passengerType, int count)
    {
        var farePerPassenger = passengerType == PassengerType.Regular ? row.RegularFare : row.DiscountedFare;
        return new FareLine(
            passengerType,
            row.RegularFare,
            row.RegularFare - farePerPassenger,
            farePerPassenger,
            count,
            farePerPassenger * count);
    }

    /// <summary>The charged row with the rows just before and after it, where the matrix has them.</summary>
    private static IReadOnlyList<FareMatrixRow> Excerpt(FareMatrix fareMatrix, int rowIndex)
    {
        var first = Math.Max(0, rowIndex - 1);
        var last = Math.Min(fareMatrix.Rows.Count - 1, rowIndex + 1);
        return fareMatrix.Rows.Skip(first).Take(last - first + 1).ToList();
    }
}

/// <summary>What a trip should cost the whole Party, and how that was reached.</summary>
/// <param name="MatrixExcerpt">
/// The charged row of the fare matrix with the row before and the row after it,
/// so the commuter can see where the fare comes from. At the matrix's first or
/// last row there is only one neighbour.
/// </param>
public sealed record FareBreakdown(
    TripDistance Distance,
    IReadOnlyList<FareLine> Lines,
    decimal TotalFare,
    IReadOnlyList<FareMatrixRow> MatrixExcerpt);

/// <summary>One passenger type's share of a <see cref="FareBreakdown"/>.</summary>
/// <param name="Discount">Regular Fare minus the fare per passenger; 0 for Regular passengers.</param>
public sealed record FareLine(
    PassengerType PassengerType,
    decimal RegularFare,
    decimal Discount,
    decimal FarePerPassenger,
    int Count,
    decimal Subtotal);

/// <summary>Thrown when a trip's Charged Distance is longer than the fare matrix's last row.</summary>
public sealed class ChargedDistanceBeyondFareMatrixException(int chargedKm, int maxKm)
    : Exception($"The Charged Distance of {chargedKm} km is beyond the fare matrix's last row ({maxKm} km).")
{
    public int ChargedKm { get; } = chargedKm;

    /// <summary>The distance of the matrix's last row: the longest trip it can price.</summary>
    public int MaxKm { get; } = maxKm;
}
