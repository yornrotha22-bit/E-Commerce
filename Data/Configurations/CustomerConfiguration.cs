using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RothaShop.Models;

namespace RothaShop.Data.Configurations;

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.Property(a => a.FullName).IsRequired().HasMaxLength(150);
        builder.Property(a => a.PhoneNumber).IsRequired().HasMaxLength(20);
        builder.Property(a => a.AddressLine1).IsRequired().HasMaxLength(300);
        builder.Property(a => a.AddressLine2).HasMaxLength(300);
        builder.Property(a => a.City).IsRequired().HasMaxLength(100);
        builder.Property(a => a.State).HasMaxLength(100);
        builder.Property(a => a.PostalCode).HasMaxLength(20);
        builder.Property(a => a.Country).IsRequired().HasMaxLength(100);

        builder.HasIndex(a => new { a.UserId, a.IsDefault });

        builder.HasOne(a => a.User)
            .WithMany(u => u.Addresses)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.Property(c => c.CartKey).HasMaxLength(64);
        builder.HasIndex(c => c.CartKey);
        builder.HasIndex(c => c.UserId);

        // One line per variant for a signed-in customer.
        builder.HasIndex(c => new { c.UserId, c.ProductVariantId })
            .IsUnique()
            .HasFilter("\"UserId\" IS NOT NULL");

        // One line per variant for a guest cart.
        builder.HasIndex(c => new { c.CartKey, c.ProductVariantId })
            .IsUnique()
            .HasFilter("\"CartKey\" IS NOT NULL");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_CartItems_Quantity", "\"Quantity\" > 0");
            t.HasCheckConstraint(
                "CK_CartItems_Owner",
                "\"UserId\" IS NOT NULL OR \"CartKey\" IS NOT NULL");
        });

        builder.HasOne(c => c.User)
            .WithMany(u => u.CartItems)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.ProductVariant)
            .WithMany(v => v.CartItems)
            .HasForeignKey(c => c.ProductVariantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class WishlistItemConfiguration : IEntityTypeConfiguration<WishlistItem>
{
    public void Configure(EntityTypeBuilder<WishlistItem> builder)
    {
        builder.HasIndex(w => new { w.UserId, w.ProductId }).IsUnique();

        builder.HasOne(w => w.User)
            .WithMany(u => u.WishlistItems)
            .HasForeignKey(w => w.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(w => w.Product)
            .WithMany(p => p.WishlistItems)
            .HasForeignKey(w => w.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.Property(c => c.Code).IsRequired().HasMaxLength(50);
        builder.Property(c => c.DiscountValue).HasColumnType(ConfigurationDefaults.Money);
        builder.Property(c => c.MinimumOrderAmount).HasColumnType(ConfigurationDefaults.Money);
        builder.Property(c => c.MaxDiscountAmount).HasColumnType(ConfigurationDefaults.Money);

        builder.HasIndex(c => c.Code).IsUnique();
        builder.HasIndex(c => new { c.IsActive, c.StartsAt, c.EndsAt });

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Coupons_DiscountValue", "\"DiscountValue\" > 0");
            t.HasCheckConstraint("CK_Coupons_UsedCount", "\"UsedCount\" >= 0");
            t.HasCheckConstraint(
                "CK_Coupons_PercentageRange",
                "\"DiscountType\" <> 0 OR \"DiscountValue\" <= 100");
        });
    }
}

public class ProductReviewConfiguration : IEntityTypeConfiguration<ProductReview>
{
    public void Configure(EntityTypeBuilder<ProductReview> builder)
    {
        builder.Property(r => r.Comment).IsRequired().HasMaxLength(2000);

        // A customer may review a given product only once.
        builder.HasIndex(r => new { r.UserId, r.ProductId }).IsUnique();
        builder.HasIndex(r => new { r.ProductId, r.IsApproved });

        builder.ToTable(t =>
            t.HasCheckConstraint("CK_ProductReviews_Rating", "\"Rating\" BETWEEN 1 AND 5"));

        builder.HasOne(r => r.Product)
            .WithMany(p => p.Reviews)
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.User)
            .WithMany(u => u.Reviews)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.OrderItem)
            .WithMany()
            .HasForeignKey(r => r.OrderItemId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
