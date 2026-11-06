using JeepneyFareCalculator.Application.FareMatrices;
using JeepneyFareCalculator.Domain.FareMatrices;
using Microsoft.EntityFrameworkCore;

namespace JeepneyFareCalculator.Infrastructure.Persistence;

/// <summary>Keeps Fare Matrix Versions in SQL Server through EF Core.</summary>
public sealed class FareMatrixStore(AppDbContext dbContext) : IFareMatrixStore
{
    public async Task<FareMatrixVersionSummary> AddAsActiveAsync(
        FareMatrix fareMatrix,
        string? note,
        DateTimeOffset uploadedAt,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Deactivate first: the unique index allows only one active version per vehicle type.
        await dbContext.FareMatrixVersions
            .Where(v => v.VehicleType == fareMatrix.VehicleType && v.IsActive)
            .ExecuteUpdateAsync(setters => setters.SetProperty(v => v.IsActive, false), cancellationToken);

        var version = new FareMatrixVersionEntity
        {
            VehicleType = fareMatrix.VehicleType,
            UploadedAt = uploadedAt.ToUniversalTime(),
            Note = note,
            IsActive = true,
            Rows = fareMatrix.Rows
                .Select(row => new FareMatrixRowEntity
                {
                    DistanceKm = row.DistanceKm,
                    RegularFare = row.RegularFare,
                    DiscountedFare = row.DiscountedFare,
                })
                .ToList(),
        };
        dbContext.FareMatrixVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new FareMatrixVersionSummary(
            version.Id,
            version.VehicleType,
            version.UploadedAt,
            version.Note,
            version.IsActive,
            version.Rows.Count);
    }

    public async Task<IReadOnlyList<FareMatrixVersionSummary>> ListVersionsAsync(
        VehicleType vehicleType,
        CancellationToken cancellationToken)
    {
        return await dbContext.FareMatrixVersions
            .AsNoTracking()
            .Where(v => v.VehicleType == vehicleType)
            .OrderByDescending(v => v.UploadedAt)
            .ThenByDescending(v => v.Id)
            .Select(v => new FareMatrixVersionSummary(
                v.Id,
                v.VehicleType,
                v.UploadedAt,
                v.Note,
                v.IsActive,
                v.Rows.Count))
            .ToListAsync(cancellationToken);
    }
}
