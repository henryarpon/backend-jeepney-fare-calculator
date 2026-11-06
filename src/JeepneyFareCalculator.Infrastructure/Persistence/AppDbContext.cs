using Microsoft.EntityFrameworkCore;

namespace JeepneyFareCalculator.Infrastructure.Persistence;

/// <summary>The application's database. It has no tables yet.</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}
