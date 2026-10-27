using System.ComponentModel.DataAnnotations;

namespace RothaShop.ViewModels.Admin;

public class AdminUserRow
{
    public string Id { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; }

    public bool IsLockedOut { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}

public class AdminUserListViewModel
{
    public string? Search { get; set; }

    public string? Role { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public int TotalItems { get; set; }

    public int TotalPages => PageSize <= 0
        ? 1
        : (int)Math.Ceiling(TotalItems / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;

    public IReadOnlyList<string> AvailableRoles { get; set; } = Array.Empty<string>();

    public IReadOnlyList<AdminUserRow> Items { get; set; } = Array.Empty<AdminUserRow>();
}

public class TeamMemberCreateViewModel
{
    [Required]
    [StringLength(150, MinimumLength = 2)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Phone]
    [StringLength(30)]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [Required]
    public string Role { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class AdminUserDetailsViewModel
{
    public string Id { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; }

    public bool EmailConfirmed { get; set; }

    public bool IsLockedOut { get; set; }

    public DateTimeOffset? LockoutEnd { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public int OrderCount { get; set; }

    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> AllRoles { get; set; } = Array.Empty<string>();

    public ResetUserPasswordViewModel ResetPassword { get; set; } = new();
}

public class ResetUserPasswordViewModel
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
