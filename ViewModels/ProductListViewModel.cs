using Microsoft.AspNetCore.Mvc.Rendering;

namespace RothaShop.ViewModels;

/// <summary>
/// Catalog query values submitted from the storefront filter form.
/// </summary>
public class ProductQuery
{
    public string? Search { get; set; }

    public int? CategoryId { get; set; }

    public int? BrandId { get; set; }

    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }

    public bool InStockOnly { get; set; }

    public string Sort { get; set; } = "newest";

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 12;
}

public class ProductListViewModel
{
    public string? Search { get; set; }

    public int? CategoryId { get; set; }

    public int? BrandId { get; set; }

    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }

    public bool InStockOnly { get; set; }

    public string Sort { get; set; } = "newest";

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 12;

    public int TotalItems { get; set; }

    public int TotalPages => PageSize <= 0
        ? 1
        : (int)Math.Ceiling(TotalItems / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;

    public int FirstItemNumber => TotalItems == 0 ? 0 : ((Page - 1) * PageSize) + 1;

    public int LastItemNumber => Math.Min(Page * PageSize, TotalItems);

    public string? ActiveCategoryName { get; set; }

    public string? ActiveBrandName { get; set; }

    public IReadOnlyList<ProductCardViewModel> Products { get; set; } = Array.Empty<ProductCardViewModel>();

    public IReadOnlyList<SelectListItem> CategoryOptions { get; set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> BrandOptions { get; set; } = Array.Empty<SelectListItem>();

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(Search)
        || CategoryId.HasValue
        || BrandId.HasValue
        || MinPrice.HasValue
        || MaxPrice.HasValue
        || InStockOnly;
}
