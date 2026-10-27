using System.ComponentModel.DataAnnotations;

namespace RothaShop.Models;

/// <summary>
/// Customer order. Customer and shipping values are copied into the order so that
/// historical records remain correct even if the profile or address changes later.
/// </summary>
public class Order
{
    public int Id { get; set; }

    [Required]
    [MaxLength(30)]
    public string OrderNumber { get; set; } = string.Empty;

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    [Required]
    [MaxLength(150)]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string CustomerPhone { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? CustomerEmail { get; set; }

    [Required]
    [MaxLength(300)]
    public string ShippingAddressLine1 { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? ShippingAddressLine2 { get; set; }

    [Required]
    [MaxLength(100)]
    public string ShippingCity { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ShippingState { get; set; }

    [MaxLength(20)]
    public string? ShippingPostalCode { get; set; }

    [Required]
    [MaxLength(100)]
    public string ShippingCountry { get; set; } = "Cambodia";

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [Range(0, 999_999_999)]
    public decimal Subtotal { get; set; }

    [Range(0, 999_999_999)]
    public decimal ShippingFee { get; set; }

    [Range(0, 999_999_999)]
    public decimal DiscountAmount { get; set; }

    [Range(0, 999_999_999)]
    public decimal TotalAmount { get; set; }

    public int? CouponId { get; set; }

    public Coupon? Coupon { get; set; }

    /// <summary>
    /// Coupon code snapshot at the time of purchase.
    /// </summary>
    [MaxLength(50)]
    public string? CouponCode { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    public OrderStatus OrderStatus { get; set; } = OrderStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ConfirmedAt { get; set; }

    public DateTime? ShippedAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    [MaxLength(500)]
    public string? CancellationReason { get; set; }

    /// <summary>
    /// Courier or delivery service name, for example "J&T Express".
    /// </summary>
    [MaxLength(100)]
    public string? ShippingCarrier { get; set; }

    /// <summary>
    /// Courier tracking code shared with the customer.
    /// </summary>
    [MaxLength(100)]
    public string? TrackingNumber { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();
}
