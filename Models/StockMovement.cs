using System.ComponentModel.DataAnnotations;

namespace RothaShop.Models;

/// <summary>
/// Append-only stock change history for a product variant.
/// </summary>
public class StockMovement
{
    public int Id { get; set; }

    public int ProductVariantId { get; set; }

    public ProductVariant? ProductVariant { get; set; }

    /// <summary>
    /// Signed change: positive for inbound stock, negative for outbound stock.
    /// </summary>
    public int QuantityChange { get; set; }

    /// <summary>
    /// Stock quantity after the change was applied.
    /// </summary>
    public int StockAfter { get; set; }

    public StockMovementType Type { get; set; }

    [MaxLength(300)]
    public string? Reason { get; set; }

    /// <summary>
    /// Optional reference such as an order number or supplier document.
    /// </summary>
    [MaxLength(100)]
    public string? Reference { get; set; }

    public string? PerformedByUserId { get; set; }

    public ApplicationUser? PerformedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
