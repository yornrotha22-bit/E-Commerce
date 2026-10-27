using System.ComponentModel.DataAnnotations;

namespace RothaShop.Models;

/// <summary>
/// Append-only audit trail of order status changes.
/// </summary>
public class OrderStatusHistory
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public Order? Order { get; set; }

    public OrderStatus? FromStatus { get; set; }

    public OrderStatus ToStatus { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }

    public string? ChangedByUserId { get; set; }

    public ApplicationUser? ChangedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
