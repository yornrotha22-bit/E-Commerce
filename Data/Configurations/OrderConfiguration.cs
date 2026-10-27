using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RothaShop.Models;

namespace RothaShop.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.Property(o => o.OrderNumber).IsRequired().HasMaxLength(30);
        builder.Property(o => o.CustomerName).IsRequired().HasMaxLength(150);
        builder.Property(o => o.CustomerPhone).IsRequired().HasMaxLength(20);
        builder.Property(o => o.CustomerEmail).HasMaxLength(256);
        builder.Property(o => o.ShippingAddressLine1).IsRequired().HasMaxLength(300);
        builder.Property(o => o.ShippingAddressLine2).HasMaxLength(300);
        builder.Property(o => o.ShippingCity).IsRequired().HasMaxLength(100);
        builder.Property(o => o.ShippingState).HasMaxLength(100);
        builder.Property(o => o.ShippingPostalCode).HasMaxLength(20);
        builder.Property(o => o.ShippingCountry).IsRequired().HasMaxLength(100);
        builder.Property(o => o.CouponCode).HasMaxLength(50);
        builder.Property(o => o.ShippingCarrier).HasMaxLength(100);
        builder.Property(o => o.TrackingNumber).HasMaxLength(100);

        builder.Property(o => o.Subtotal).HasColumnType(ConfigurationDefaults.Money);
        builder.Property(o => o.ShippingFee).HasColumnType(ConfigurationDefaults.Money);
        builder.Property(o => o.DiscountAmount).HasColumnType(ConfigurationDefaults.Money);
        builder.Property(o => o.TotalAmount).HasColumnType(ConfigurationDefaults.Money);

        builder.HasIndex(o => o.OrderNumber).IsUnique();
        builder.HasIndex(o => new { o.UserId, o.CreatedAt });
        builder.HasIndex(o => new { o.OrderStatus, o.CreatedAt });
        builder.HasIndex(o => new { o.PaymentStatus, o.CreatedAt });
        builder.HasIndex(o => o.CreatedAt);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Orders_Subtotal", "\"Subtotal\" >= 0");
            t.HasCheckConstraint("CK_Orders_ShippingFee", "\"ShippingFee\" >= 0");
            t.HasCheckConstraint("CK_Orders_DiscountAmount", "\"DiscountAmount\" >= 0");
            t.HasCheckConstraint("CK_Orders_TotalAmount", "\"TotalAmount\" >= 0");
        });

        // Customers with order history must not be hard-deleted.
        builder.HasOne(o => o.User)
            .WithMany(u => u.Orders)
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Coupon)
            .WithMany(c => c.Orders)
            .HasForeignKey(o => o.CouponId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.Property(i => i.ProductName).IsRequired().HasMaxLength(200);
        builder.Property(i => i.SKU).IsRequired().HasMaxLength(64);
        builder.Property(i => i.VariantDescription).HasMaxLength(150);
        builder.Property(i => i.ImageUrl).HasMaxLength(500);

        builder.Property(i => i.UnitPrice).HasColumnType(ConfigurationDefaults.Money);
        builder.Property(i => i.LineTotal).HasColumnType(ConfigurationDefaults.Money);

        builder.HasIndex(i => i.OrderId);
        builder.HasIndex(i => i.ProductVariantId);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_OrderItems_Quantity", "\"Quantity\" > 0");
            t.HasCheckConstraint("CK_OrderItems_UnitPrice", "\"UnitPrice\" >= 0");
            t.HasCheckConstraint("CK_OrderItems_LineTotal", "\"LineTotal\" >= 0");
        });

        builder.HasOne(i => i.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Financial history must never lose its product reference.
        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.ProductVariant)
            .WithMany(v => v.OrderItems)
            .HasForeignKey(i => i.ProductVariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.Property(h => h.Note).HasMaxLength(500);
        builder.HasIndex(h => new { h.OrderId, h.CreatedAt });

        builder.HasOne(h => h.Order)
            .WithMany(o => o.StatusHistory)
            .HasForeignKey(h => h.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.ChangedByUser)
            .WithMany()
            .HasForeignKey(h => h.ChangedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.Property(p => p.Provider).IsRequired().HasMaxLength(50);
        builder.Property(p => p.TransactionReference).HasMaxLength(120);
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(3);
        builder.Property(p => p.FailureReason).HasMaxLength(500);
        builder.Property(p => p.ProviderMessage).HasMaxLength(2000);
        builder.Property(p => p.Amount).HasColumnType(ConfigurationDefaults.Money);

        builder.HasIndex(p => p.OrderId);
        builder.HasIndex(p => p.Status);

        // A provider transaction may only be recorded once (webhook idempotency).
        builder.HasIndex(p => new { p.Provider, p.TransactionReference })
            .IsUnique()
            .HasFilter("\"TransactionReference\" IS NOT NULL");

        builder.ToTable(t => t.HasCheckConstraint("CK_Payments_Amount", "\"Amount\" > 0"));

        builder.HasOne(p => p.Order)
            .WithMany(o => o.Payments)
            .HasForeignKey(p => p.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
