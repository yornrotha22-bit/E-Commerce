using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RothaShop.Data;
using RothaShop.Localization;
using RothaShop.Models;
using RothaShop.ViewModels;

namespace RothaShop.Services;

/// <summary>
/// Read-only catalog queries for the public storefront.
/// Products, prices and stock always come from the database.
/// </summary>
public interface ICatalogService
{
    Task<ProductListViewModel> SearchAsync(ProductQuery query, CancellationToken cancellationToken = default);

    Task<ProductDetailsViewModel?> GetProductDetailsAsync(
        int id,
        string? userId = null,
        CancellationToken cancellationToken = default);

    Task<HomeViewModel> GetHomeAsync(CancellationToken cancellationToken = default);
}

public class CatalogService : ICatalogService
{
    public const int CatalogPageSize = 12;
    private const int HomeSectionSize = 8;

    private readonly ApplicationDbContext _dbContext;
    private readonly ITranslator _t;

    public CatalogService(ApplicationDbContext dbContext, ITranslator translator)
    {
        _dbContext = dbContext;
        _t = translator;
    }

    /// <summary>
    /// Shared card projection so every storefront grid returns the same shape.
    /// </summary>
    private static readonly Expression<Func<Product, ProductCardViewModel>> CardProjection =
        product => new ProductCardViewModel
        {
            Id = product.Id,
            Name = product.Name,
            Slug = product.Slug,
            BrandName = product.Brand != null ? product.Brand.Name : string.Empty,
            CategoryName = product.Category != null ? product.Category.Name : string.Empty,
            // A product with gallery images but no explicit main image still shows a picture.
            ImageUrl = product.MainImageUrl
                ?? product.Images.OrderBy(i => i.DisplayOrder).Select(i => i.Url).FirstOrDefault(),
            MinPrice = product.Variants
                .Where(v => v.IsActive)
                .Select(v => (decimal?)v.Price)
                .Min() ?? 0m,
            MaxPrice = product.Variants
                .Where(v => v.IsActive)
                .Select(v => (decimal?)v.Price)
                .Max() ?? 0m,
            HasDiscount = product.Variants.Any(v =>
                v.IsActive && v.CompareAtPrice.HasValue && v.CompareAtPrice > v.Price),
            InStock = product.Variants.Any(v => v.IsActive && v.StockQuantity > 0),
            DefaultVariantId = product.Variants
                .Where(v => v.IsActive)
                .OrderBy(v => v.StockQuantity > 0 ? 0 : 1)
                .ThenBy(v => v.Price)
                .Select(v => (int?)v.Id)
                .FirstOrDefault(),
            IsFeatured = product.IsFeatured,
            ReviewCount = product.Reviews.Count(r => r.IsApproved),
            AverageRating = product.Reviews
                .Where(r => r.IsApproved)
                .Select(r => (double?)r.Rating)
                .Average()
        };

    public async Task<ProductListViewModel> SearchAsync(
        ProductQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = query.PageSize <= 0 ? CatalogPageSize : Math.Min(query.PageSize, 48);

        var products = StorefrontProducts();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            var pattern = SearchPattern.Contains(term);

            products = products.Where(p =>
                EF.Functions.ILike(p.Name, pattern)
                || EF.Functions.ILike(p.Brand!.Name, pattern)
                || EF.Functions.ILike(p.Category!.Name, pattern)
                || p.Variants.Any(v => EF.Functions.ILike(v.SKU, pattern)));
        }

        if (query.CategoryId.HasValue)
        {
            products = products.Where(p => p.CategoryId == query.CategoryId.Value);
        }

        if (query.BrandId.HasValue)
        {
            products = products.Where(p => p.BrandId == query.BrandId.Value);
        }

        if (query.MinPrice is > 0)
        {
            products = products.Where(p =>
                p.Variants.Where(v => v.IsActive).Select(v => (decimal?)v.Price).Min() >= query.MinPrice.Value);
        }

        if (query.MaxPrice is > 0)
        {
            products = products.Where(p =>
                p.Variants.Where(v => v.IsActive).Select(v => (decimal?)v.Price).Min() <= query.MaxPrice.Value);
        }

        if (query.InStockOnly)
        {
            products = products.Where(p =>
                p.Variants.Any(v => v.IsActive && v.StockQuantity > 0));
        }

        var totalItems = await products.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)pageSize);
        page = Math.Min(page, totalPages);

        products = query.Sort switch
        {
            "oldest" => products.OrderBy(p => p.CreatedAt),
            "name" => products.OrderBy(p => p.Name),
            "price_asc" => products.OrderBy(p =>
                p.Variants.Where(v => v.IsActive).Select(v => (decimal?)v.Price).Min() ?? 0m),
            "price_desc" => products.OrderByDescending(p =>
                p.Variants.Where(v => v.IsActive).Select(v => (decimal?)v.Price).Min() ?? 0m),
            "popular" => products.OrderByDescending(p =>
                p.Variants.SelectMany(v => v.OrderItems).Sum(i => (int?)i.Quantity) ?? 0)
                .ThenByDescending(p => p.CreatedAt),
            _ => products.OrderByDescending(p => p.CreatedAt)
        };

        var items = await products
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(CardProjection)
            .ToListAsync(cancellationToken);

        var model = new ProductListViewModel
        {
            Search = query.Search,
            CategoryId = query.CategoryId,
            BrandId = query.BrandId,
            MinPrice = query.MinPrice,
            MaxPrice = query.MaxPrice,
            InStockOnly = query.InStockOnly,
            Sort = string.IsNullOrWhiteSpace(query.Sort) ? "newest" : query.Sort,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            Products = items,
            CategoryOptions = await GetCategoryOptionsAsync(query.CategoryId, cancellationToken),
            BrandOptions = await GetBrandOptionsAsync(query.BrandId, cancellationToken)
        };

        if (query.CategoryId.HasValue)
        {
            model.ActiveCategoryName = await _dbContext.Categories.AsNoTracking()
                .Where(c => c.Id == query.CategoryId.Value)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (query.BrandId.HasValue)
        {
            model.ActiveBrandName = await _dbContext.Brands.AsNoTracking()
                .Where(b => b.Id == query.BrandId.Value)
                .Select(b => b.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return model;
    }

    public async Task<ProductDetailsViewModel?> GetProductDetailsAsync(
        int id,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var product = await StorefrontProducts()
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .Include(p => p.Reviews.Where(r => r.IsApproved))
                .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            return null;
        }

        var variants = product.Variants
            .Where(v => v.IsActive)
            .OrderBy(v => v.Price)
            .ThenBy(v => v.Id)
            .ToList();

        var imageUrls = new List<string>();

        if (!string.IsNullOrWhiteSpace(product.MainImageUrl))
        {
            imageUrls.Add(product.MainImageUrl);
        }

        imageUrls.AddRange(product.Images
            .OrderBy(i => i.DisplayOrder)
            .Select(i => i.Url)
            .Where(url => !string.Equals(url, product.MainImageUrl, StringComparison.Ordinal)));

        var reviews = product.Reviews
            .Where(r => r.IsApproved)
            .OrderByDescending(r => r.CreatedAt)
            .Take(20)
            .ToList();

        var reviewCount = await _dbContext.ProductReviews.AsNoTracking()
            .CountAsync(r => r.ProductId == id && r.IsApproved, cancellationToken);

        var relatedProducts = await StorefrontProducts()
            .Where(p => p.CategoryId == product.CategoryId && p.Id != product.Id)
            .OrderByDescending(p => p.IsFeatured)
            .ThenByDescending(p => p.CreatedAt)
            .Take(4)
            .Select(CardProjection)
            .ToListAsync(cancellationToken);

        int? reviewOrderItemId = null;
        string? reviewMessage = null;

        if (string.IsNullOrEmpty(userId))
        {
            reviewMessage = _t["msg.reviewSignIn"];
        }
        else
        {
            var alreadyReviewed = await _dbContext.ProductReviews.AsNoTracking()
                .AnyAsync(r => r.UserId == userId && r.ProductId == id, cancellationToken);

            if (alreadyReviewed)
            {
                reviewMessage = _t["msg.reviewDuplicate"];
            }
            else
            {
                reviewOrderItemId = await _dbContext.OrderItems.AsNoTracking()
                    .Where(i => i.ProductId == id
                        && i.Order!.UserId == userId
                        && i.Order.OrderStatus == OrderStatus.Delivered)
                    .OrderByDescending(i => i.Id)
                    .Select(i => (int?)i.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                if (!reviewOrderItemId.HasValue)
                {
                    reviewMessage = _t["msg.reviewNeedsDeliveredOrder"];
                }
            }
        }

        return new ProductDetailsViewModel
        {
            Product = product,
            Variants = variants,
            ImageUrls = imageUrls,
            Reviews = reviews,
            ReviewCount = reviewCount,
            AverageRating = reviewCount > 0
                ? await _dbContext.ProductReviews.AsNoTracking()
                    .Where(r => r.ProductId == id && r.IsApproved)
                    .AverageAsync(r => (double)r.Rating, cancellationToken)
                : null,
            TotalStock = variants.Sum(v => v.StockQuantity),
            MinPrice = variants.Count > 0 ? variants.Min(v => v.Price) : 0m,
            MaxPrice = variants.Count > 0 ? variants.Max(v => v.Price) : 0m,
            RelatedProducts = relatedProducts,
            ReviewOrderItemId = reviewOrderItemId,
            CanSubmitReview = reviewOrderItemId.HasValue,
            ReviewMessage = reviewMessage
        };
    }

    public async Task<HomeViewModel> GetHomeAsync(CancellationToken cancellationToken = default)
    {
        var products = StorefrontProducts();

        var featured = await products
            .Where(p => p.IsFeatured)
            .OrderByDescending(p => p.CreatedAt)
            .Take(HomeSectionSize)
            .Select(CardProjection)
            .ToListAsync(cancellationToken);

        if (featured.Count == 0)
        {
            featured = await products
                .OrderByDescending(p => p.CreatedAt)
                .Take(HomeSectionSize)
                .Select(CardProjection)
                .ToListAsync(cancellationToken);
        }

        var newArrivals = await products
            .OrderByDescending(p => p.CreatedAt)
            .Take(HomeSectionSize)
            .Select(CardProjection)
            .ToListAsync(cancellationToken);

        var bestSellers = await products
            .OrderByDescending(p =>
                p.Variants.SelectMany(v => v.OrderItems).Sum(i => (int?)i.Quantity) ?? 0)
            .ThenByDescending(p => p.CreatedAt)
            .Take(HomeSectionSize)
            .Select(CardProjection)
            .ToListAsync(cancellationToken);

        var categories = await _dbContext.Categories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategorySummary
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                ImageUrl = c.ImageUrl,
                ProductCount = c.Products.Count(p =>
                    p.IsActive && p.Variants.Any(v => v.IsActive))
            })
            // Every active category is shown so a category added in the admin
            // panel appears in the store immediately, even before it has products.
            .Take(12)
            .ToListAsync(cancellationToken);

        var brands = await _dbContext.Brands.AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new BrandSummary
            {
                Id = b.Id,
                Name = b.Name,
                Slug = b.Slug,
                LogoUrl = b.LogoUrl,
                ProductCount = b.Products.Count(p =>
                    p.IsActive && p.Variants.Any(v => v.IsActive))
            })
            // Same rule as categories: an active brand is always listed.
            .Take(14)
            .ToListAsync(cancellationToken);

        var settings = await _dbContext.StoreSettings.AsNoTracking()
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        var freeShippingThreshold = decimal.TryParse(
            GetSetting(settings, "FreeShippingThreshold", "50.00"),
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsedThreshold)
            ? parsedThreshold
            : 50m;

        return new HomeViewModel
        {
            FeaturedProducts = featured,
            NewArrivals = newArrivals,
            BestSellers = bestSellers,
            Categories = categories,
            Brands = brands,
            StoreName = GetSetting(settings, "StoreName", "KStore") ?? "KStore",
            FreeShippingThreshold = freeShippingThreshold,
            SupportPhone = GetSetting(settings, "SupportPhone", null),
            SupportEmail = GetSetting(settings, "SupportEmail", null)
        };
    }

    /// <summary>
    /// Products that may appear publicly: active product, active brand/category and at least one active variant.
    /// </summary>
    private IQueryable<Product> StorefrontProducts()
    {
        return _dbContext.Products.AsNoTracking()
            .Where(p => p.IsActive
                && p.Brand!.IsActive
                && p.Category!.IsActive
                && p.Variants.Any(v => v.IsActive));
    }

    private async Task<IReadOnlyList<SelectListItem>> GetCategoryOptionsAsync(
        int? selectedCategoryId,
        CancellationToken cancellationToken)
    {
        var categories = await _dbContext.Categories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new SelectListItem(
                c.Name,
                c.Id.ToString(),
                selectedCategoryId.HasValue && c.Id == selectedCategoryId.Value))
            .ToListAsync(cancellationToken);

        return categories;
    }

    private async Task<IReadOnlyList<SelectListItem>> GetBrandOptionsAsync(
        int? selectedBrandId,
        CancellationToken cancellationToken)
    {
        var brands = await _dbContext.Brands.AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new SelectListItem(
                b.Name,
                b.Id.ToString(),
                selectedBrandId.HasValue && b.Id == selectedBrandId.Value))
            .ToListAsync(cancellationToken);

        return brands;
    }

    private static string? GetSetting(
        IReadOnlyDictionary<string, string?> settings,
        string key,
        string? fallback)
    {
        return settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }
}
