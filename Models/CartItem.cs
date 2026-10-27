using System.ComponentModel.DataAnnotations;

namespace RothaShop.Models;

/// <summary>
/// Cart line. It belongs either to a signed-in user (UserId) or to a guest cart (CartKey).
/// Prices are never stored here; they are always resolved from ProductVariant on the server.
/// </summary>
public class CartItem
{
    public int Id { get; set; }

    public string? UserId { get; set; }

    public ApplicationUser? User { get; set; }

    /// <summary>
    /// Opaque guest cart identifier stored in a session/cookie (max 64 characters).
    /// </summary>
    [MaxLength(64)]
    public string? CartKey { get; set; }

    public int ProductVariantId { get; set; }

    public ProductVariant? ProductVariant { get; set; }

    [Range(1, 999)]
    public int Quantity { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
