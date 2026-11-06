using JeepneyFareCalculator.Domain.FareMatrices;

namespace JeepneyFareCalculator.Infrastructure.Persistence;

/// <summary>A row of the <c>FareMatrixVersion</c> table: one uploaded Fare Matrix.</summary>
public sealed class FareMatrixVersionEntity
{
    public int Id { get; set; }
    public VehicleType VehicleType { get; set; }

    /// <summary>When it was uploaded, always in UTC.</summary>
    public DateTimeOffset UploadedAt { get; set; }

    public string? Note { get; set; }

    /// <summary>Whether this is the vehicle type's Active Fare Matrix. At most one per vehicle type.</summary>
    public bool IsActive { get; set; }

    public List<FareMatrixRowEntity> Rows { get; set; } = [];
}

/// <summary>A row of the <c>FareMatrixRow</c> table: the fares for one distance of one version.</summary>
public sealed class FareMatrixRowEntity
{
    public int FareMatrixVersionId { get; set; }
    public int DistanceKm { get; set; }
    public decimal RegularFare { get; set; }
    public decimal DiscountedFare { get; set; }
}
