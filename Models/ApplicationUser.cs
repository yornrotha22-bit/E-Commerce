using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace RothaShop.Models;

/// <summary>
/// Application user. Public registration always assigns only the Customer role.
/// </summary>
public class ApplicationUser : IdentityUser
{
    [Required]
    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Public relative URL of the profile photo, or null when the customer
    /// has not uploaded one yet.
    /// </summary>
    [MaxLength(300)]
    public string? AvatarUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    /// <summary>Best-effort device and browser description from the sign-in request.</summary>
    [MaxLength(200)]
    public string? LastLoginDevice { get; set; }

    /// <summary>
    /// Disabled accounts cannot sign in. Accounts are deactivated instead of deleted
    /// when they have order history.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public ICollection<Address> Addresses { get; set; } = new List<Address>();

    public ICollection<Order> Orders { get; set; } = new List<Order>();

    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();

    public ICollection<ProductReview> Reviews { get; set; } = new List<ProductReview>();
}
