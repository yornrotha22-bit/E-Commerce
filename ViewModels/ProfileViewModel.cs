using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
namespace RothaShop.ViewModels;
public class ProfileViewModel
{
    [Required(ErrorMessage = "Enter your full name.")]
    [StringLength(150)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;
    [Phone(ErrorMessage = "Enter a valid phone number.")]
    [StringLength(20)]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }
    /// <summary>
    /// Shown read-only. Changing the sign-in email is handled separately.
    /// </summary>
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;
    public DateTime MemberSince { get; set; }

    /// <summary>
    /// Optional new profile photo. Validated and stored by IImageService.
    /// </summary>
    [Display(Name = "Profile photo")]
    public IFormFile? Avatar { get; set; }

    /// <summary>Current photo URL, used to render the preview.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>When checked, the stored photo is deleted on save.</summary>
    [Display(Name = "Remove photo")]
    public bool RemoveAvatar { get; set; }
}
public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Enter your current password.")]
    [DataType(DataType.Password)]
    [Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;
    [Required(ErrorMessage = "Enter a new password.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Use at least 8 characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;
    [Required(ErrorMessage = "Confirm the new password.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
