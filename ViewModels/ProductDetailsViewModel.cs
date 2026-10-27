using RothaShop.Models;

namespace RothaShop.ViewModels;

public class ProductDetailsViewModel
{
    public Product Product { get; set; } = new();

    /// <summary>
    /// Only active variants are purchasable and shown.
    /// </summary>
    public IReadOnlyList<ProductVariant> Variants { get; set; } = Array.Empty<ProductVariant>();

    /// <summary>
    /// Main image followed by gallery images.
    /// </summary>
    public IReadOnlyList<string> ImageUrls { get; set; } = Array.Empty<string>();

    public IReadOnlyList<ProductReview> Reviews { get; set; } = Array.Empty<ProductReview>();

    public double? AverageRating { get; set; }

    public int ReviewCount { get; set; }

    public int TotalStock { get; set; }

    public decimal MinPrice { get; set; }

    public decimal MaxPrice { get; set; }

    public bool InStock => TotalStock > 0;

    public IReadOnlyList<ProductCardViewModel> RelatedProducts { get; set; } = Array.Empty<ProductCardViewModel>();

    public bool CanSubmitReview { get; set; }

    public string? ReviewMessage { get; set; }

    public int? ReviewOrderItemId { get; set; }

    /// <summary>
    /// True when the signed-in customer saved this product. Guests always false.
    /// </summary>
    public bool IsInWishlist { get; set; }
}
