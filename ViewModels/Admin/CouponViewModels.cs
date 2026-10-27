using System.ComponentModel.DataAnnotations;
using RothaShop.Models;

namespace RothaShop.ViewModels.Admin;

public class AdminCouponRow
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public DiscountType DiscountType { get; set; }

    public decimal DiscountValue { get; set; }

    public decimal MinimumOrderAmount { get; set; }

    public int? UsageLimit { get; set; }

    public int UsedCount { get; set; }

    public DateTime StartsAt { get; set; }

    public DateTime? EndsAt { get; set; }

    public bool IsActive { get; set; }
}

public class CouponFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Enter a coupon code.")]
    [StringLength(50)]
    [Display(Name = "Coupon code")]
    public string Code { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Display(Name = "Discount type")]
    public DiscountType DiscountType { get; set; } = DiscountType.Percentage;

    [Range(0.01, 999_999_999, ErrorMessage = "Enter a discount value greater than zero.")]
    [Display(Name = "Discount value")]
    public decimal DiscountValue { get; set; }

    [Range(0, 999_999_999)]
    [Display(Name = "Minimum order amount")]
    public decimal MinimumOrderAmount { get; set; }

    [Range(0.01, 999_999_999)]
    [Display(Name = "Maximum discount (optional)")]
    public decimal? MaxDiscountAmount { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Starts on (UTC)")]
    public DateTime StartsAt { get; set; } = DateTime.UtcNow.Date;

    [DataType(DataType.Date)]
    [Display(Name = "Ends on (UTC, optional)")]
    public DateTime? EndsAt { get; set; }

    [Range(1, 1_000_000)]
    [Display(Name = "Total usage limit (optional)")]
    public int? UsageLimit { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public int UsedCount { get; set; }
}
