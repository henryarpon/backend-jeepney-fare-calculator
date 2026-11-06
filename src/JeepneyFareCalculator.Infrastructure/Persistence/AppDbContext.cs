using JeepneyFareCalculator.Application.FareMatrices;
using Microsoft.EntityFrameworkCore;

namespace JeepneyFareCalculator.Infrastructure.Persistence;

/// <summary>The application's database: the uploaded Fare Matrix Versions and their rows.</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<FareMatrixVersionEntity> FareMatrixVersions => Set<FareMatrixVersionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FareMatrixVersionEntity>(version =>
        {
            version.ToTable("FareMatrixVersion");
            version.HasKey(v => v.Id);

            // Stored by name ("TraditionalJeepney") so the table reads well on its own.
            version.Property(v => v.VehicleType).HasConversion<string>().HasMaxLength(32);
            version.Property(v => v.Note).HasMaxLength(UploadFareMatrix.NoteMaxLength);

            // At most one Active Fare Matrix per vehicle type.
            version.HasIndex(v => v.VehicleType)
                .IsUnique()
                .HasFilter("[IsActive] = 1");

            version.HasMany(v => v.Rows)
                .WithOne()
                .HasForeignKey(r => r.FareMatrixVersionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FareMatrixRowEntity>(row =>
        {
            row.ToTable("FareMatrixRow");
            row.HasKey(r => new { r.FareMatrixVersionId, r.DistanceKm });
            row.Property(r => r.RegularFare).HasPrecision(8, 2);
            row.Property(r => r.DiscountedFare).HasPrecision(8, 2);
        });
    }
}
