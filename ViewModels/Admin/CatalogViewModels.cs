using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace RothaShop.ViewModels.Admin;

public class BrandFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Enter the brand name.")]
    [StringLength(150)]
    [Display(Name = "Brand name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(160)]
    public string? Slug { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    // Populated from the database for the existing-logo preview; never trusted as posted input.
    public string? LogoUrl { get; set; }

    [Display(Name = "Logo image")]
    public IFormFile? LogoFile { get; set; }

    public bool RemoveLogo { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}

public class CategoryFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Enter the category name.")]
    [StringLength(150)]
    [Display(Name = "Category name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(160)]
    public string? Slug { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(500)]
    [Display(Name = "Image URL")]
    [Url(ErrorMessage = "Enter a valid absolute URL.")]
    public string? ImageUrl { get; set; }

    [Display(Name = "Parent category")]
    public int? ParentId { get; set; }

    [Range(0, 999)]
    [Display(Name = "Display order")]
    public int DisplayOrder { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public IReadOnlyList<SelectListItem> ParentOptions { get; set; } = Array.Empty<SelectListItem>();
}

public class AdminProductListViewModel
{
    public string? Search { get; set; }

    [Display(Name = "Category")]
    public int? CategoryId { get; set; }

    [Display(Name = "Brand")]
    public int? BrandId { get; set; }

    [Display(Name = "Status")]
    public bool? IsActive { get; set; }

    public string Sort { get; set; } = "newest";

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 15;

    public int TotalItems { get; set; }

    public int TotalPages => PageSize <= 0
        ? 1
        : (int)Math.Ceiling(TotalItems / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;

    public IReadOnlyList<AdminProductRow> Items { get; set; } = Array.Empty<AdminProductRow>();

    public IReadOnlyList<SelectListItem> CategoryOptions { get; set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> BrandOptions { get; set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> StatusOptions { get; set; } = Array.Empty<SelectListItem>();
}

public class AdminProductRow
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string BrandName { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public string? MainImageUrl { get; set; }

    public bool IsActive { get; set; }

    public bool IsFeatured { get; set; }

    /// <summary>Mirrors <c>CatalogService.StorefrontProducts()</c> so the admin can see
    /// why a product is missing from the shop.</summary>
    public bool IsVisibleOnStorefront { get; set; }

    public int VariantCount { get; set; }

    public int TotalStock { get; set; }

    public decimal MinPrice { get; set; }

    public decimal MaxPrice { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class ProductFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Enter the product name.")]
    [StringLength(200)]
    [Display(Name = "Product name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(220)]
    public string? Slug { get; set; }

    [StringLength(4000)]
    public string? Description { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Choose a brand.")]
    [Display(Name = "Brand")]
    public int BrandId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Choose a category.")]
    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Featured product")]
    public bool IsFeatured { get; set; }

    public string? SimilarProductConfirmation { get; set; }

    public IReadOnlyList<SimilarProductMatch> SimilarProducts { get; set; } = Array.Empty<SimilarProductMatch>();

    public string? MainImageUrl { get; set; }

    [Display(Name = "Remove current main image")]
    public bool RemoveMainImage { get; set; }

    [Display(Name = "Main image")]
    public IFormFile? MainImage { get; set; }

    [Display(Name = "Gallery images")]
    public List<IFormFile>? GalleryImages { get; set; }

    public IReadOnlyList<ProductImageInfo> ExistingImages { get; set; } = Array.Empty<ProductImageInfo>();

    public List<VariantFormItem> Variants { get; set; } = new() { new VariantFormItem() };

    public IReadOnlyList<SelectListItem> BrandOptions { get; set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> CategoryOptions { get; set; } = Array.Empty<SelectListItem>();
}

public class SimilarProductMatch
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public class ProductImportViewModel
{
    public IFormFile? File { get; set; }

    public int ImportedProducts { get; set; }

    public int ImportedVariants { get; set; }

    public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
}

public class ProductImageInfo
{
    public int Id { get; set; }

    public string Url { get; set; } = string.Empty;
}

public class VariantFormItem
{
    public int Id { get; set; }

    [Required(ErrorMessage = "SKU is required.")]
    [StringLength(64)]
    [Display(Name = "SKU")]
    public string SKU { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Color { get; set; }

    [StringLength(50)]
    public string? Storage { get; set; }

    [StringLength(500)]
    [Display(Name = "Variant image URL")]
    public string? ImageUrl { get; set; }

    [Display(Name = "Variant image upload")]
    public IFormFile? ImageFile { get; set; }

    [Range(0.01, 999_999_999, ErrorMessage = "Enter a price greater than zero.")]
    public decimal Price { get; set; }

    [Range(0.01, 999_999_999, ErrorMessage = "Compare-at price must be greater than zero.")]
    [Display(Name = "Compare-at price")]
    public decimal? CompareAtPrice { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Stock cannot be negative.")]
    [Display(Name = "Stock")]
    public int StockQuantity { get; set; }

    [Range(0, 100_000)]
    [Display(Name = "Low-stock alert")]
    public int LowStockThreshold { get; set; } = 5;

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public bool IsNew => Id == 0;
}
