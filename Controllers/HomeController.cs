using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RothaShop.Data;
using RothaShop.Localization;
using RothaShop.Services;
using RothaShop.ViewModels;

namespace RothaShop.Controllers;

public class HomeController : Controller
{
    private readonly ICatalogService _catalogService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ITranslator _t;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        ICatalogService catalogService,
        ApplicationDbContext dbContext,
        ITranslator translator,
        ILogger<HomeController> logger)
    {
        _catalogService = catalogService;
        _dbContext = dbContext;
        _t = translator;
        _logger = logger;
    }

    /// <summary>
    /// Database-driven storefront home page.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await _catalogService.GetHomeAsync(cancellationToken);
        await MarkWishlistProductsAsync(model, cancellationToken);

        return View(model);
    }

    /// <summary>
    /// Store policies linked from the footer. The wording matches what the store
    /// actually implements today (no refunds module, manual bank transfer, etc.).
    /// </summary>
    [HttpGet]
    public IActionResult Policies()
    {
        ViewData["Title"] = _t["policies.title"];
        return View();
    }

    private async Task MarkWishlistProductsAsync(
        HomeViewModel model,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
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

        foreach (var product in model.FeaturedProducts
                     .Concat(model.NewArrivals)
                     .Concat(model.BestSellers))
        {
            product.IsInWishlist = saved.Contains(product.Id);
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? code = null)
    {
        var statusCode = code ?? StatusCodes.Status500InternalServerError;
        Response.StatusCode = statusCode;

        return View(new ErrorViewModel
        {
            StatusCode = statusCode,
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}
