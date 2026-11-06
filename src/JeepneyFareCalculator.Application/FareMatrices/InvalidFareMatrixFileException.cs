namespace JeepneyFareCalculator.Application.FareMatrices;

/// <summary>
/// One reason an uploaded fare matrix file was rejected.
/// </summary>
/// <param name="Row">
/// The row of the file, counting the header as row 1 like a spreadsheet does.
/// <c>null</c> when the error is about the whole file, e.g. an empty one.
/// </param>
/// <param name="Column">The column the error is in, e.g. <c>RegularFare</c>, or <c>null</c> for the whole row.</param>
/// <param name="Message">What is wrong, written for the Administrator.</param>
public sealed record FareMatrixFileError(int? Row, string? Column, string Message);

/// <summary>
/// Thrown when an uploaded fare matrix file is invalid. It lists every error, not
/// just the first. A rejected file is never saved, not even in part.
/// </summary>
public sealed class InvalidFareMatrixFileException(IReadOnlyList<FareMatrixFileError> errors)
    : Exception($"The fare matrix file has {errors.Count} error(s).")
{
    public IReadOnlyList<FareMatrixFileError> Errors { get; } = errors;
}
