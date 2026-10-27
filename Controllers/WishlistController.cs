using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RothaShop.Data;
using RothaShop.Localization;
using RothaShop.Models;
using RothaShop.ViewModels;

namespace RothaShop.Controllers;

/// <summary>
/// Saved products for the signed-in customer. Wishlist is account-only, so guests
/// are redirected to sign in and returned to the page they came from.
/// </summary>
[Authorize]
public class WishlistController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITranslator _t;

    public WishlistController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ITranslator translator)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _t = translator;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var items = await _dbContext.WishlistItems.AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new WishlistRowViewModel
            {
                ProductId = w.ProductId,
                Name = w.Product!.Name,
                BrandName = w.Product.Brand != null ? w.Product.Brand.Name : string.Empty,
                ImageUrl = w.Product.MainImageUrl
                    ?? w.Product.Images.OrderBy(i => i.DisplayOrder).Select(i => i.Url).FirstOrDefault(),
                MinPrice = w.Product.Variants
                    .Where(v => v.IsActive)
                    .Select(v => (decimal?)v.Price)
                    .Min() ?? 0m,
                InStock = w.Product.Variants.Any(v => v.IsActive && v.StockQuantity > 0),
                CreatedAt = w.CreatedAt
            })
            .ToListAsync(cancellationToken);

        ViewData["Title"] = _t["wishlist.title"];
        ViewData["AccountTab"] = "Wishlist";
        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(
        int productId,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        if (!await _dbContext.Products.AnyAsync(p => p.Id == productId, cancellationToken))
        {
            return NotFound();
        }

        var existing = await _dbContext.WishlistItems
            .FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId, cancellationToken);

        if (existing is null)
        {
            _dbContext.WishlistItems.Add(new WishlistItem
            {
                UserId = userId,
                ProductId = productId,
                CreatedAt = DateTime.UtcNow
            });

            TempData["WishlistMessage"] = _t["msg.wishlistSaved"];
        }
        else
        {
            _dbContext.WishlistItems.Remove(existing);
            TempData["WishlistMessage"] = _t["msg.wishlistRemoved"];
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }
}
