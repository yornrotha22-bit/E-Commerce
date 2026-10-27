using System.ComponentModel.DataAnnotations;

namespace RothaShop.Models;

/// <summary>
/// Additional gallery image for a product. The primary image is stored on Product.MainImageUrl.
/// </summary>
public class ProductImage
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    [Required]
    [MaxLength(500)]
    public string Url { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? AltText { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsMain { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
