using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RothaShop.Models;

namespace RothaShop.Data.Configurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.Property(m => m.Reason).HasMaxLength(300);
        builder.Property(m => m.Reference).HasMaxLength(100);

        builder.HasIndex(m => new { m.ProductVariantId, m.CreatedAt });

        builder.ToTable(t =>
            t.HasCheckConstraint("CK_StockMovements_StockAfter", "\"StockAfter\" >= 0"));

        builder.HasOne(m => m.ProductVariant)
            .WithMany(v => v.StockMovements)
            .HasForeignKey(m => m.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.PerformedByUser)
            .WithMany()
            .HasForeignKey(m => m.PerformedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.Property(a => a.Action).IsRequired().HasMaxLength(100);
        builder.Property(a => a.EntityName).IsRequired().HasMaxLength(100);
        builder.Property(a => a.EntityId).HasMaxLength(50);
        builder.Property(a => a.Details).HasMaxLength(2000);
        builder.Property(a => a.IpAddress).HasMaxLength(64);

        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => new { a.EntityName, a.EntityId });
        builder.HasIndex(a => a.UserId);

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class StoreSettingConfiguration : IEntityTypeConfiguration<StoreSetting>
{
    public void Configure(EntityTypeBuilder<StoreSetting> builder)
    {
        builder.Property(s => s.Key).IsRequired().HasMaxLength(100);
        builder.Property(s => s.Value).HasMaxLength(2000);
        builder.Property(s => s.Description).HasMaxLength(300);

        builder.HasIndex(s => s.Key).IsUnique();
    }
}
