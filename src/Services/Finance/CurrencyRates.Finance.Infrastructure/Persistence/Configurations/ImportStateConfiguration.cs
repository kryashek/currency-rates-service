using CurrencyRates.Finance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CurrencyRates.Finance.Infrastructure.Persistence.Configurations;

public sealed class ImportStateConfiguration : IEntityTypeConfiguration<ImportState>
{
    public void Configure(EntityTypeBuilder<ImportState> builder)
    {
        builder.ToTable("import_state", table =>
        {
            table.HasCheckConstraint("ck_import_state_singleton", "id = 1");
            table.HasCheckConstraint("ck_import_state_version", "snapshot_version BETWEEN 0 AND 18446744073709551615");
            table.HasCheckConstraint("ck_import_state_values", "(snapshot_version = 0 AND source_date IS NULL AND loaded_at IS NULL) OR (snapshot_version > 0 AND source_date IS NOT NULL AND source_date BETWEEN DATE '0001-01-01' AND DATE '9999-12-31' AND loaded_at IS NOT NULL AND isfinite(loaded_at))");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.SnapshotVersion).HasColumnName("snapshot_version").HasConversion<decimal>().HasPrecision(20, 0);
        builder.Property(x => x.SourceDate).HasColumnName("source_date");
        builder.Property(x => x.LoadedAt).HasColumnName("loaded_at");
        builder.HasData(new ImportState());
    }
}
