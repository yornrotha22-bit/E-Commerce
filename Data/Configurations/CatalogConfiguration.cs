using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RothaShop.Models;

namespace RothaShop.Data.Configurations;

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.Property(b => b.Name).IsRequired().HasMaxLength(150);
        builder.Property(b => b.Slug).IsRequired().HasMaxLength(160);

        builder.HasIndex(b => b.Name).IsUnique();
        builder.HasIndex(b => b.Slug).IsUnique();
        builder.HasIndex(b => b.IsActive);
    }
}

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.Property(c => c.Name).IsRequired().HasMaxLength(150);
        builder.Property(c => c.Slug).IsRequired().HasMaxLength(160);

        builder.HasIndex(c => c.Slug).IsUnique();
        builder.HasIndex(c => new { c.ParentId, c.DisplayOrder });
        builder.HasIndex(c => c.IsActive);

        builder.HasOne(c => c.Parent)
            .WithMany(c => c.Children)
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Slug).IsRequired().HasMaxLength(220);
        builder.Property(p => p.MainImageUrl).HasMaxLength(500);

        builder.HasIndex(p => p.Slug).IsUnique();
        builder.HasIndex(p => new { p.CategoryId, p.IsActive });
        builder.HasIndex(p => new { p.BrandId, p.IsActive });
        builder.HasIndex(p => p.Name);
        builder.HasIndex(p => p.IsFeatured);

        builder.HasOne(p => p.Brand)
            .WithMany(b => b.Products)
            .HasForeignKey(p => p.BrandId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.Property(v => v.SKU).IsRequired().HasMaxLength(64);
        builder.Property(v => v.Price).HasColumnType(ConfigurationDefaults.Money);
        builder.Property(v => v.CompareAtPrice).HasColumnType(ConfigurationDefaults.Money);

        // ProductVariant.Version is a [Timestamp] uint: Npgsql maps it to the
        // PostgreSQL xmin system column, which PostgreSQL bumps on every update.

        builder.HasIndex(v => v.SKU).IsUnique();
        builder.HasIndex(v => new { v.ProductId, v.IsActive });
        builder.HasIndex(v => v.StockQuantity);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_ProductVariants_Price", "\"Price\" >= 0");
            t.HasCheckConstraint("CK_ProductVariants_StockQuantity", "\"StockQuantity\" >= 0");
            t.HasCheckConstraint(
                "CK_ProductVariants_CompareAtPrice",
                "\"CompareAtPrice\" IS NULL OR \"CompareAtPrice\" >= \"Price\"");
        });

        builder.HasOne(v => v.Product)
            .WithMany(p => p.Variants)
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.Property(i => i.Url).IsRequired().HasMaxLength(500);
        builder.HasIndex(i => new { i.ProductId, i.DisplayOrder });

        builder.HasOne(i => i.Product)
            .WithMany(p => p.Images)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
