using CurrencyRates.Finance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CurrencyRates.Finance.Infrastructure.Persistence.Configurations;

public sealed class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        builder.ToTable("currency", table =>
        {
            table.HasCheckConstraint("ck_currency_id", "id COLLATE \"C\" ~ '^R[0-9]{1,31}$'");
            table.HasCheckConstraint("ck_currency_code", "code COLLATE \"C\" ~ '^[A-Z]{3}$'");
            table.HasCheckConstraint("ck_currency_name", "name ~ U&'[^\\0009\\000A\\000B\\000C\\000D\\0020\\0085\\00A0\\1680\\2000\\2001\\2002\\2003\\2004\\2005\\2006\\2007\\2008\\2009\\200A\\2028\\2029\\202F\\205F\\3000]'");
            table.HasCheckConstraint("ck_currency_rate", "(is_available AND rate IS NOT NULL AND rate > 0 AND rate < 1000000000000) OR (NOT is_available AND rate IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(32).ValueGeneratedNever();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(3).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.IsAvailable).HasColumnName("is_available");
        builder.Property(x => x.Rate).HasColumnName("rate").HasPrecision(20, 8);
        builder.HasIndex(x => x.Code).IsUnique().HasFilter("is_available");
    }
}
