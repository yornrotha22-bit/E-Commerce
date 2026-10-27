using System.ComponentModel.DataAnnotations;

namespace RothaShop.Models;

public class Product
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(220)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    public int BrandId { get; set; }

    public Brand? Brand { get; set; }

    public int CategoryId { get; set; }

    public Category? Category { get; set; }

    /// <summary>
    /// Relative path of the main image, for example "/uploads/products/abc.webp".
    /// </summary>
    [MaxLength(500)]
    public string? MainImageUrl { get; set; }

    /// <summary>
    /// Soft availability flag. Products referenced by orders are deactivated, never deleted.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public bool IsFeatured { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();

    public ICollection<ProductReview> Reviews { get; set; } = new List<ProductReview>();

    public ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();
}
