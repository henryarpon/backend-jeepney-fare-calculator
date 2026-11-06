namespace JeepneyFareCalculator.Domain.FareMatrices;

/// <summary>A column of a <see cref="FareMatrix"/>.</summary>
public enum FareMatrixColumn
{
    DistanceKm,
    RegularFare,
    DiscountedFare,
}

/// <summary>One rule of a <see cref="FareMatrix"/> that its rows break.</summary>
/// <param name="RowIndex">
/// The position of the row in the rows given, starting at 0, or <c>null</c> when
/// the rule is about the whole matrix (e.g. it has no rows).
/// </param>
/// <param name="Column">The column the problem is in, or <c>null</c> for the whole row.</param>
/// <param name="Message">What is wrong, written for the Administrator.</param>
public sealed record FareMatrixRuleViolation(int? RowIndex, FareMatrixColumn? Column, string Message);

/// <summary>
/// Thrown when rows that break the Fare Matrix rules are used to create one.
/// It lists every broken rule, not just the first.
/// </summary>
public sealed class InvalidFareMatrixException(IReadOnlyList<FareMatrixRuleViolation> violations)
    : Exception($"The fare matrix breaks {violations.Count} rule(s).")
{
    public IReadOnlyList<FareMatrixRuleViolation> Violations { get; } = violations;
}
