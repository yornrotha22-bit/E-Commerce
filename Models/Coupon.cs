using System.ComponentModel.DataAnnotations;

namespace RothaShop.Models;

public class Coupon
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public DiscountType DiscountType { get; set; }

    /// <summary>
    /// Percentage value (for example 10 = 10%) or a fixed amount in store currency.
    /// </summary>
    [Range(0.01, 999_999_999)]
    public decimal DiscountValue { get; set; }

    [Range(0, 999_999_999)]
    public decimal MinimumOrderAmount { get; set; }

    /// <summary>
    /// Caps a percentage discount. Null means no cap.
    /// </summary>
    [Range(0.01, 999_999_999)]
    public decimal? MaxDiscountAmount { get; set; }

    public DateTime StartsAt { get; set; } = DateTime.UtcNow;

    public DateTime? EndsAt { get; set; }

    /// <summary>
    /// Total number of times the coupon may be used. Null means unlimited.
    /// </summary>
    public int? UsageLimit { get; set; }

    public int UsedCount { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
