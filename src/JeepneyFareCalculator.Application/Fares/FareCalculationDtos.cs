using JeepneyFareCalculator.Application.FareMatrices;
using JeepneyFareCalculator.Domain.FareMatrices;
using JeepneyFareCalculator.Domain.Fares;

namespace JeepneyFareCalculator.Application.Fares;

/// <summary>
/// Body of <c>POST /api/fares/calculate</c>. Every field is nullable so that a
/// missing one is reported by <see cref="CalculateFareRequestValidator"/> instead
/// of silently becoming 0 or the first vehicle type.
/// </summary>
public sealed record CalculateFareRequest(
    VehicleType? VehicleType,
    CoordinatesDto? Origin,
    CoordinatesDto? Destination,
    IReadOnlyList<PartyMemberDto>? Party);

/// <summary>A point on the map in decimal degrees.</summary>
public sealed record CoordinatesDto(double? Lat, double? Lng);

/// <summary>How many passengers of one passenger type are in the Party.</summary>
public sealed record PartyMemberDto(PassengerType? PassengerType, int? Count);

/// <summary>Body of a successful <c>POST /api/fares/calculate</c>: what the trip should cost the Party.</summary>
/// <param name="EstimatedDistanceKm">The Estimated Distance in km, to 1 decimal place.</param>
/// <param name="ChargedDistanceKm">The Charged Distance in whole km.</param>
/// <param name="Lines">One line per passenger type with at least one passenger, Regular first.</param>
/// <param name="MatrixExcerpt">The charged row with the row before and after it, where the matrix has them.</param>
public sealed record FareBreakdownDto(
    decimal EstimatedDistanceKm,
    int ChargedDistanceKm,
    IReadOnlyList<FareLineDto> Lines,
    decimal TotalFare,
    FareMatrixVersionInfo FareMatrixVersion,
    IReadOnlyList<FareMatrixRowDto> MatrixExcerpt)
{
    public static FareBreakdownDto From(FareBreakdown breakdown, FareMatrixVersionInfo version) => new(
        breakdown.Distance.EstimatedKm,
        breakdown.Distance.ChargedKm,
        breakdown.Lines
            .Select(line => new FareLineDto(
                line.PassengerType,
                line.RegularFare,
                line.Discount,
                line.FarePerPassenger,
                line.Count,
                line.Subtotal))
            .ToList(),
        breakdown.TotalFare,
        version,
        breakdown.MatrixExcerpt.Select(FareMatrixRowDto.From).ToList());
}

/// <summary>One passenger type's share of the Total Fare.</summary>
public sealed record FareLineDto(
    PassengerType PassengerType,
    decimal RegularFare,
    decimal Discount,
    decimal FarePerPassenger,
    int Count,
    decimal Subtotal);
