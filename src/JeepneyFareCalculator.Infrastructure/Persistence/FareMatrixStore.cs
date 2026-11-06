using System.Linq.Expressions;
using JeepneyFareCalculator.Application.FareMatrices;
using JeepneyFareCalculator.Domain.FareMatrices;
using Microsoft.EntityFrameworkCore;

namespace JeepneyFareCalculator.Infrastructure.Persistence;

/// <summary>Keeps Fare Matrix Versions in SQL Server through EF Core.</summary>
public sealed class FareMatrixStore(AppDbContext dbContext) : IFareMatrixStore
{
    /// <summary>Reads a version as a summary, counting its rows in the database instead of loading them.</summary>
    private static readonly Expression<Func<FareMatrixVersionEntity, FareMatrixVersionSummary>> ToSummary =
        v => new FareMatrixVersionSummary(v.Id, v.VehicleType, v.UploadedAt, v.Note, v.IsActive, v.Rows.Count);

    public async Task<FareMatrixVersionSummary> AddAsActiveAsync(
        FareMatrix fareMatrix,
        string? note,
        DateTimeOffset uploadedAt,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await DeactivateActiveVersionAsync(fareMatrix.VehicleType, cancellationToken);

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
            .Select(ToSummary)
            .ToListAsync(cancellationToken);
    }

    public async Task<FareMatrixVersionSummary?> ActivateAsync(int id, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var version = await dbContext.FareMatrixVersions
            .AsNoTracking()
            .Where(v => v.Id == id)
            .Select(ToSummary)
            .SingleOrDefaultAsync(cancellationToken);
        if (version is null || version.IsActive)
        {
            return version;
        }

        await DeactivateActiveVersionAsync(version.VehicleType, cancellationToken);
        await dbContext.FareMatrixVersions
            .Where(v => v.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(v => v.IsActive, true), cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return version with { IsActive = true };
    }

    public async Task<FareMatrixVersionDetails?> GetVersionAsync(int id, CancellationToken cancellationToken)
    {
        return await dbContext.FareMatrixVersions
            .AsNoTracking()
            .Where(v => v.Id == id)
            .Select(v => new FareMatrixVersionDetails(
                v.Id,
                v.VehicleType,
                v.UploadedAt,
                v.Note,
                v.IsActive,
                v.Rows
                    .OrderBy(r => r.DistanceKm)
                    .Select(r => new FareMatrixRowDto(r.DistanceKm, r.RegularFare, r.DiscountedFare))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Deactivates the vehicle type's active version, if it has one. Run it before
    /// activating another version: the unique index allows only one active version
    /// per vehicle type, even inside a transaction.
    /// </summary>
    private Task DeactivateActiveVersionAsync(VehicleType vehicleType, CancellationToken cancellationToken)
    {
        return dbContext.FareMatrixVersions
            .Where(v => v.VehicleType == vehicleType && v.IsActive)
            .ExecuteUpdateAsync(setters => setters.SetProperty(v => v.IsActive, false), cancellationToken);
    }
}
