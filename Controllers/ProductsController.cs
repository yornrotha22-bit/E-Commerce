using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RothaShop.Data;
using RothaShop.Localization;
using RothaShop.Services;
using RothaShop.ViewModels;

namespace RothaShop.Controllers;

public class ProductsController : Controller
{
    private readonly ICatalogService _catalogService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ITranslator _t;

    public ProductsController(
        ICatalogService catalogService,
        ApplicationDbContext dbContext,
        ITranslator translator)
    {
        _catalogService = catalogService;
        _dbContext = dbContext;
        _t = translator;
    }

    [HttpGet]
    public async Task<IActionResult> Index(ProductQuery query, CancellationToken cancellationToken)
    {
        var model = await _catalogService.SearchAsync(query, cancellationToken);
        await MarkWishlistProductsAsync(model, cancellationToken);

        ViewData["Title"] = model.ActiveCategoryName
            ?? (string.IsNullOrWhiteSpace(model.Search)
                ? _t["products.allProducts"]
                : _t.Format("products.searchResultsFor", model.Search));

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var model = await _catalogService.GetProductDetailsAsync(id, userId, cancellationToken);

        if (model is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            model.IsInWishlist = await _dbContext.WishlistItems.AsNoTracking()
                .AnyAsync(w => w.UserId == userId && w.ProductId == model.Product.Id, cancellationToken);
        }

        ViewData["Title"] = model.Product.Name;
        return View(model);
    }

    private async Task MarkWishlistProductsAsync(
        ProductListViewModel model,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId) || model.Products.Count == 0)
        {
            return;
        }

        var savedIds = await _dbContext.WishlistItems.AsNoTracking()
            .Where(w => w.UserId == userId)
            .Select(w => w.ProductId)
            .ToListAsync(cancellationToken);

        if (savedIds.Count == 0)
        {
            return;
        }

        var saved = savedIds.ToHashSet();

        foreach (var product in model.Products)
        {
            product.IsInWishlist = saved.Contains(product.Id);
        }
    }
}
