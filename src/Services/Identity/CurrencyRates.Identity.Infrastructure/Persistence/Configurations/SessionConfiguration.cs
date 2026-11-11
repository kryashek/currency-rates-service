using CurrencyRates.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CurrencyRates.Identity.Infrastructure.Persistence.Configurations;

public sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("session", table =>
        {
            table.HasCheckConstraint("ck_session_id", "id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("ck_session_times", "isfinite(created_at) AND isfinite(expires_at) AND expires_at > created_at AND (revoked_at IS NULL OR (isfinite(revoked_at) AND revoked_at >= created_at))");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.UserId);
    }
}
