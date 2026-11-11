using CurrencyRates.Finance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CurrencyRates.Finance.Infrastructure.Persistence.Configurations;

public sealed class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> builder)
    {
        builder.ToTable("favorite", table => table.HasCheckConstraint("ck_favorite_user_id", "user_id <> '00000000-0000-0000-0000-000000000000'::uuid"));
        builder.HasKey(x => new { x.UserId, x.CurrencyId });
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.CurrencyId).HasColumnName("currency_id").HasMaxLength(32);
        builder.HasOne<Currency>().WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.CurrencyId);
    }
}
