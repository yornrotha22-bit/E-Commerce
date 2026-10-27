using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RothaShop.Models;

namespace RothaShop.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Brand> Brands => Set<Brand>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();

    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    public DbSet<Address> Addresses => Set<Address>();

    public DbSet<CartItem> CartItems => Set<CartItem>();

    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();

    public DbSet<Coupon> Coupons => Set<Coupon>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<StoreSetting> StoreSettings => Set<StoreSetting>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // All IEntityTypeConfiguration classes in this assembly are applied automatically.
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
