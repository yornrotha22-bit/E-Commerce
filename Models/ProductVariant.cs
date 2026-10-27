using System.ComponentModel.DataAnnotations;

namespace RothaShop.Models;

public class ProductVariant
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    /// <summary>
    /// Unique stock keeping unit, for example "IPH15-128-BLK".
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string SKU { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Color { get; set; }

    /// <summary>
    /// Storage capacity such as "128GB", "256GB" or "1TB".
    /// </summary>
    [MaxLength(50)]
    public string? Storage { get; set; }

    /// <summary>Optional image shown when this variant is selected.</summary>
    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    [Range(0.01, 999_999_999)]
    public decimal Price { get; set; }

    /// <summary>
    /// Original price used to display a discount. Null when the product is not discounted.
    /// </summary>
    [Range(0.01, 999_999_999)]
    public decimal? CompareAtPrice { get; set; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }

    public int LowStockThreshold { get; set; } = 5;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optimistic concurrency token mapped to the PostgreSQL <c>xmin</c> system column.
    /// PostgreSQL updates it automatically on every row change, so concurrent stock
    /// updates conflict instead of overselling.
    /// </summary>
    [Timestamp]
    public uint Version { get; set; }

    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();

    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
