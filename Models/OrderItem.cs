using System.ComponentModel.DataAnnotations;

namespace RothaShop.Models;

/// <summary>
/// Historical order line. Product name, SKU and price are snapshots taken when the order was placed.
/// </summary>
public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public Order? Order { get; set; }

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    public int ProductVariantId { get; set; }

    public ProductVariant? ProductVariant { get; set; }

    [Required]
    [MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string SKU { get; set; } = string.Empty;

    /// <summary>
    /// Human readable variant description, for example "Color: Black / Storage: 128GB".
    /// </summary>
    [MaxLength(150)]
    public string? VariantDescription { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    [Range(0.01, 999_999_999)]
    public decimal UnitPrice { get; set; }

    [Range(1, 999)]
    public int Quantity { get; set; }

    [Range(0, 999_999_999)]
    public decimal LineTotal { get; set; }
}
