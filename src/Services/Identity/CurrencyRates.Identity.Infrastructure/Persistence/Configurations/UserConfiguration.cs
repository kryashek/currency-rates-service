using CurrencyRates.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CurrencyRates.Identity.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("user", table =>
        {
            table.HasCheckConstraint("ck_user_id", "id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("ck_user_name", "name COLLATE \"C\" ~ '^[A-Za-z0-9_.-]{3,50}$'");
            table.HasCheckConstraint("ck_user_normalized_name", "normalized_name = upper(name COLLATE \"C\")");
            table.HasCheckConstraint("ck_user_password_hash", "length(btrim(password_hash)) > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
        builder.Property(x => x.NormalizedName).HasColumnName("normalized_name").HasMaxLength(50).UseCollation("C").IsRequired();
        builder.Property(x => x.PasswordHash).HasColumnName("password_hash").IsRequired();
        builder.HasIndex(x => x.NormalizedName).IsUnique();
    }
}
