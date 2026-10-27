using System.ComponentModel.DataAnnotations;

namespace RothaShop.ViewModels;

public class AddressFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Enter the recipient name.")]
    [StringLength(150)]
    [Display(Name = "Recipient name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a phone number.")]
    [Phone(ErrorMessage = "Enter a valid phone number.")]
    [StringLength(20)]
    [Display(Name = "Phone number")]
    public string PhoneNumber { get; set; } = string.Empty;

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

    [Display(Name = "Use as my default shipping address")]
    public bool IsDefault { get; set; }
}
