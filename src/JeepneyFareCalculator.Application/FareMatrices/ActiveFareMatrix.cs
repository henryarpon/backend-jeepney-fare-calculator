using JeepneyFareCalculator.Domain.FareMatrices;

namespace JeepneyFareCalculator.Application.FareMatrices;

/// <summary>Which Fare Matrix Version priced a trip, shown to the commuter for transparency.</summary>
public sealed record FareMatrixVersionInfo(int Id, DateTimeOffset UploadedAt, string? Note);

/// <summary>A vehicle type's Active Fare Matrix together with the version it comes from.</summary>
public sealed record ActiveFareMatrix(FareMatrixVersionInfo Version, FareMatrix FareMatrix);
