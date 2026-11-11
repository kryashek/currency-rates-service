using CurrencyRates.Finance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CurrencyRates.Finance.Infrastructure.Persistence.Configurations;

public sealed class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.ToTable("import_batch", table =>
        {
            table.HasCheckConstraint("ck_import_batch_id", "batch_id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("ck_import_batch_hash", "octet_length(request_hash) = 32");
            table.HasCheckConstraint("ck_import_batch_versions", "expected_snapshot_version BETWEEN 0 AND 18446744073709551615 AND snapshot_version BETWEEN 1 AND 18446744073709551615");
            table.HasCheckConstraint("ck_import_batch_result", "(outcome = 1 AND snapshot_version = expected_snapshot_version + 1 AND state_source_date = source_date) OR (outcome = 2 AND snapshot_version = expected_snapshot_version AND state_source_date = source_date) OR (outcome = 3 AND snapshot_version = expected_snapshot_version AND source_date < state_source_date)");
            table.HasCheckConstraint("ck_import_batch_times", "source_date BETWEEN DATE '0001-01-01' AND DATE '9999-12-31' AND state_source_date BETWEEN DATE '0001-01-01' AND DATE '9999-12-31' AND isfinite(fetched_at) AND isfinite(state_loaded_at)");
        });
        builder.HasKey(x => x.BatchId);
        builder.Property(x => x.BatchId).HasColumnName("batch_id").ValueGeneratedNever();
        builder.Property(x => x.RequestHash).HasColumnName("request_hash").IsRequired();
        builder.Property(x => x.ExpectedSnapshotVersion).HasColumnName("expected_snapshot_version").HasConversion<decimal>().HasPrecision(20, 0);
        builder.Property(x => x.SourceDate).HasColumnName("source_date");
        builder.Property(x => x.FetchedAt).HasColumnName("fetched_at");
        builder.Property(x => x.Outcome).HasColumnName("outcome").HasConversion<int>();
        builder.Property(x => x.SnapshotVersion).HasColumnName("snapshot_version").HasConversion<decimal>().HasPrecision(20, 0);
        builder.Property(x => x.StateSourceDate).HasColumnName("state_source_date");
        builder.Property(x => x.StateLoadedAt).HasColumnName("state_loaded_at");
    }
}
