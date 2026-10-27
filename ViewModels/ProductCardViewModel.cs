namespace RothaShop.ViewModels;

/// <summary>
/// Lightweight product projection used by storefront grids (home, catalog, related products).
/// </summary>
public class ProductCardViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string BrandName { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public decimal MinPrice { get; set; }

    public decimal MaxPrice { get; set; }

    public bool HasDiscount { get; set; }

    public bool InStock { get; set; }

    /// <summary>
    /// Cheapest active variant, preferring one that is in stock. Used by the
    /// "Add to cart" action on product cards.
    /// </summary>
    public int? DefaultVariantId { get; set; }

    /// <summary>
    /// True when the signed-in customer already saved this product. Filled by the
    /// controller for the current user; guests always see false.
    /// </summary>
    public bool IsInWishlist { get; set; }

    public bool IsFeatured { get; set; }

    public int ReviewCount { get; set; }

    public double? AverageRating { get; set; }

    public bool HasPriceRange => MaxPrice > MinPrice;
}
