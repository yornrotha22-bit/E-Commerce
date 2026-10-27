using System.ComponentModel.DataAnnotations;
using RothaShop.Models;

namespace RothaShop.ViewModels;

public class CheckoutViewModel
{
    public CartViewModel Cart { get; set; } = new();

    public IReadOnlyList<Address> SavedAddresses { get; set; } = Array.Empty<Address>();

    [Display(Name = "Use a saved address")]
    public int? SelectedAddressId { get; set; }

    [Required(ErrorMessage = "Enter the recipient name.")]
    [StringLength(150)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a phone number.")]
    [Phone(ErrorMessage = "Enter a valid phone number.")]
    [StringLength(20)]
    [Display(Name = "Phone number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(256)]
    [Display(Name = "Email (optional)")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Enter the street address.")]
    [StringLength(300)]
    [Display(Name = "Address line 1")]
    public string AddressLine1 { get; set; } = string.Empty;

    [StringLength(300)]
    [Display(Name = "Address line 2 (optional)")]
    public string? AddressLine2 { get; set; }

    [Required(ErrorMessage = "Enter the city or province.")]
    [StringLength(100)]
    [Display(Name = "City / Province")]
    public string City { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "State / District")]
    public string? State { get; set; }

    [StringLength(20)]
    [Display(Name = "Postal code")]
    public string? PostalCode { get; set; }

    [Required(ErrorMessage = "Enter the country.")]
    [StringLength(100)]
    public string Country { get; set; } = "Cambodia";

    [StringLength(1000)]
    [Display(Name = "Order notes (optional)")]
    public string? Notes { get; set; }

    [Display(Name = "Payment method")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CashOnDelivery;

    public bool OnlinePaymentAvailable { get; set; }

    public string? OnlinePaymentUnavailableReason { get; set; }

    [Display(Name = "Save this address to my account")]
    public bool SaveAddress { get; set; } = true;

    public string? CouponCode { get; set; }

    public string? CouponMessage { get; set; }

    public bool CouponApplied { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal Total => Math.Max(0m, Cart.Subtotal + Cart.ShippingFee - DiscountAmount);
}
