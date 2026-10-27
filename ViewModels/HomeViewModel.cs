namespace RothaShop.ViewModels;

public class HomeViewModel
{
    public IReadOnlyList<ProductCardViewModel> FeaturedProducts { get; set; } = Array.Empty<ProductCardViewModel>();

    public IReadOnlyList<ProductCardViewModel> NewArrivals { get; set; } = Array.Empty<ProductCardViewModel>();

    public IReadOnlyList<ProductCardViewModel> BestSellers { get; set; } = Array.Empty<ProductCardViewModel>();

    public IReadOnlyList<CategorySummary> Categories { get; set; } = Array.Empty<CategorySummary>();

    public IReadOnlyList<BrandSummary> Brands { get; set; } = Array.Empty<BrandSummary>();

    public string StoreName { get; set; } = "KShop";

    public decimal FreeShippingThreshold { get; set; } = 50m;

    public string? SupportPhone { get; set; }

    public string? SupportEmail { get; set; }
}

public class CategorySummary
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public int ProductCount { get; set; }
}

public class BrandSummary
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? LogoUrl { get; set; }

    public int ProductCount { get; set; }
}

public class StoreInfoViewModel
{
    public string StoreName { get; set; } = "KShop";

    public string Currency { get; set; } = "USD";

    public string? SupportPhone { get; set; }

    public string? SupportEmail { get; set; }
}
