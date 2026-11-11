using CurrencyRates.Finance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CurrencyRates.Finance.Infrastructure.Persistence;

public sealed class FinanceDbContext(DbContextOptions<FinanceDbContext> options) : DbContext(options)
{
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<ImportState> ImportStates => Set<ImportState>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("finance");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinanceDbContext).Assembly);
    }
}
