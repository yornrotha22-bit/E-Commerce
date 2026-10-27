using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RothaShop.Data;
using RothaShop.Localization;
using RothaShop.Models;
using RothaShop.ViewModels;

namespace RothaShop.Services;

public interface ICartService
{
    Task<CartViewModel> GetCartAsync(CancellationToken cancellationToken = default);

    Task<CartOperationResult> AddAsync(
        int productVariantId,
        int quantity,
        CancellationToken cancellationToken = default);

    Task<CartOperationResult> UpdateQuantityAsync(
        int cartItemId,
        int quantity,
        CancellationToken cancellationToken = default);

    Task<CartOperationResult> RemoveAsync(int cartItemId, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves guest cart lines into the signed-in customer's cart.
    /// </summary>
    Task MergeGuestCartAsync(string userId, CancellationToken cancellationToken = default);

    Task<int> GetItemCountAsync(CancellationToken cancellationToken = default);
}

public class CartOperationResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public static CartOperationResult Ok(string message) => new() { Success = true, Message = message };

    public static CartOperationResult Fail(string message) => new() { Success = false, Message = message };
}

/// <summary>
/// Database-backed cart. Prices and stock are always read from the database,
/// never from the browser.
/// </summary>
public class CartService : ICartService
{
    private const string CartCookieName = "RothaShop.Cart";
    private const int MaxQuantityPerLine = 999;

    private readonly ApplicationDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CartService> _logger;
    private readonly ITranslator _t;

    public CartService(
        ApplicationDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        ILogger<CartService> logger,
        ITranslator translator)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _t = translator;
    }

    public async Task<CartViewModel> GetCartAsync(CancellationToken cancellationToken = default)
    {
        var items = await OwnedCartItems()
            .AsNoTracking()
            .Include(c => c.ProductVariant)
                .ThenInclude(v => v!.Product)
                    .ThenInclude(p => p!.Images)
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync(cancellationToken);

        var rows = new List<CartItemViewModel>(items.Count);
        var subtotal = 0m;

        foreach (var item in items)
        {
            var variant = item.ProductVariant;
            var product = variant?.Product;

            var isAvailable = variant is not null
                && variant.IsActive
                && product is not null
                && product.IsActive
                && variant.StockQuantity >= item.Quantity;

            string? reason = null;

            if (!isAvailable)
            {
                reason = variant is null
                    ? "This variant is no longer available."
                    : product is null || !product.IsActive || !variant.IsActive
                        ? "This product is no longer available."
                        : variant.StockQuantity <= 0
                            ? "Out of stock."
                            : $"Only {variant.StockQuantity} left in stock.";
            }

            var label = BuildVariantLabel(variant);

            rows.Add(new CartItemViewModel
            {
                Id = item.Id,
                ProductId = product?.Id ?? 0,
                ProductVariantId = item.ProductVariantId,
                ProductName = product?.Name ?? "Unavailable product",
                Slug = product?.Slug ?? string.Empty,
                VariantLabel = label,
                ImageUrl = product is null
                    ? null
                    : product.MainImageUrl
                      ?? product.Images.OrderBy(i => i.DisplayOrder).Select(i => i.Url).FirstOrDefault(),
                UnitPrice = variant?.Price ?? 0m,
                Quantity = item.Quantity,
                StockQuantity = variant?.StockQuantity ?? 0,
                IsAvailable = isAvailable,
                UnavailableReason = reason
            });

            if (isAvailable)
            {
                subtotal += (variant!.Price * item.Quantity);
            }
        }

        subtotal = Math.Round(subtotal, 2, MidpointRounding.AwayFromZero);

        var (shippingFee, freeShippingThreshold) = await GetShippingSettingsAsync(cancellationToken);

        var shipping = subtotal <= 0 || subtotal >= freeShippingThreshold
            ? 0m
            : shippingFee;

        return new CartViewModel
        {
            Items = rows,
            Subtotal = subtotal,
            ShippingFee = Math.Round(shipping, 2, MidpointRounding.AwayFromZero),
            FreeShippingThreshold = freeShippingThreshold
        };
    }

    public async Task<CartOperationResult> AddAsync(
        int productVariantId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        quantity = Math.Clamp(quantity, 1, MaxQuantityPerLine);

        var variant = await _dbContext.ProductVariants
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == productVariantId, cancellationToken);

        if (variant?.Product is null || !variant.IsActive || !variant.Product.IsActive)
        {
            return CartOperationResult.Fail(_t["msg.productUnavailable"]);
        }

        if (variant.StockQuantity <= 0)
        {
            return CartOperationResult.Fail(_t["msg.variantOutOfStock"]);
        }

        var userId = CurrentUserId;
        var cartKey = string.IsNullOrEmpty(userId) ? GetCartKey(createIfMissing: true) : null;

        if (string.IsNullOrEmpty(userId) && string.IsNullOrEmpty(cartKey))
        {
            return CartOperationResult.Fail(_t["msg.cartCookie"]);
        }

        var now = DateTime.UtcNow;

        var existing = userId is not null
            ? await _dbContext.CartItems.FirstOrDefaultAsync(
                c => c.UserId == userId && c.ProductVariantId == productVariantId, cancellationToken)
            : await _dbContext.CartItems.FirstOrDefaultAsync(
                c => c.CartKey == cartKey && c.ProductVariantId == productVariantId, cancellationToken);

        if (existing is null)
        {
            _dbContext.CartItems.Add(new CartItem
            {
                UserId = userId,
                CartKey = cartKey,
                ProductVariantId = productVariantId,
                Quantity = Math.Min(quantity, variant.StockQuantity),
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            var newQuantity = Math.Min(existing.Quantity + quantity, Math.Min(variant.StockQuantity, MaxQuantityPerLine));
            existing.Quantity = newQuantity;
            existing.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return CartOperationResult.Ok(_t.Format("msg.addedToCart", variant.Product.Name));
    }

    public async Task<CartOperationResult> UpdateQuantityAsync(
        int cartItemId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        var item = await OwnedCartItems()
            .FirstOrDefaultAsync(c => c.Id == cartItemId, cancellationToken);

        if (item is null)
        {
            return CartOperationResult.Fail(_t["msg.itemNotFound"]);
        }

        if (quantity <= 0)
        {
            _dbContext.CartItems.Remove(item);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return CartOperationResult.Ok(_t["msg.removedFromCart"]);
        }

        quantity = Math.Min(quantity, MaxQuantityPerLine);

        var variant = await _dbContext.ProductVariants
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == item.ProductVariantId, cancellationToken);

        if (variant?.Product is null || !variant.IsActive || !variant.Product.IsActive)
        {
            return CartOperationResult.Fail(_t["msg.productRemoved"]);
        }

        if (quantity > variant.StockQuantity)
        {
            return CartOperationResult.Fail(_t.Format("msg.onlyInStock", variant.StockQuantity));
        }

        item.Quantity = quantity;
        item.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CartOperationResult.Ok(_t["msg.cartUpdated"]);
    }

    public async Task<CartOperationResult> RemoveAsync(
        int cartItemId,
        CancellationToken cancellationToken = default)
    {
        var item = await OwnedCartItems()
            .FirstOrDefaultAsync(c => c.Id == cartItemId, cancellationToken);

        if (item is null)
        {
            return CartOperationResult.Fail(_t["msg.itemNotFound"]);
        }

        _dbContext.CartItems.Remove(item);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CartOperationResult.Ok(_t["msg.removedFromCart"]);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        var items = await OwnedCartItems().ToListAsync(cancellationToken);

        if (items.Count == 0)
        {
            return;
        }

        _dbContext.CartItems.RemoveRange(items);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MergeGuestCartAsync(string userId, CancellationToken cancellationToken = default)
    {
        var context = _httpContextAccessor.HttpContext;
        var cartKey = context?.Request.Cookies[CartCookieName];

        if (context is null || string.IsNullOrWhiteSpace(cartKey))
        {
            return;
        }

        var guestItems = await _dbContext.CartItems
            .Where(c => c.CartKey == cartKey)
            .ToListAsync(cancellationToken);

        if (guestItems.Count == 0)
        {
            DeleteCartCookie(context);
            return;
        }

        var userItems = await _dbContext.CartItems
            .Where(c => c.UserId == userId)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;

        foreach (var guestItem in guestItems)
        {
            var existing = userItems.FirstOrDefault(i => i.ProductVariantId == guestItem.ProductVariantId);

            if (existing is null)
            {
                guestItem.UserId = userId;
                guestItem.CartKey = null;
                guestItem.UpdatedAt = now;
            }
            else
            {
                existing.Quantity = Math.Min(existing.Quantity + guestItem.Quantity, MaxQuantityPerLine);
                existing.UpdatedAt = now;
                _dbContext.CartItems.Remove(guestItem);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        DeleteCartCookie(context);

        _logger.LogInformation("Guest cart merged into cart of user {UserId}.", userId);
    }

    public async Task<int> GetItemCountAsync(CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserId;

        if (!string.IsNullOrEmpty(userId))
        {
            return await _dbContext.CartItems
                .Where(c => c.UserId == userId)
                .SumAsync(c => (int?)c.Quantity, cancellationToken) ?? 0;
        }

        var cartKey = GetCartKey(createIfMissing: false);

        if (string.IsNullOrEmpty(cartKey))
        {
            return 0;
        }

        return await _dbContext.CartItems
            .Where(c => c.CartKey == cartKey)
            .SumAsync(c => (int?)c.Quantity, cancellationToken) ?? 0;
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private string? CurrentUserId =>
        _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    private IQueryable<CartItem> OwnedCartItems()
    {
        var userId = CurrentUserId;

        if (!string.IsNullOrEmpty(userId))
        {
            return _dbContext.CartItems.Where(c => c.UserId == userId);
        }

        var cartKey = GetCartKey(createIfMissing: false);

        return string.IsNullOrEmpty(cartKey)
            ? _dbContext.CartItems.Where(c => false)
            : _dbContext.CartItems.Where(c => c.CartKey == cartKey);
    }

    private string? GetCartKey(bool createIfMissing)
    {
        var context = _httpContextAccessor.HttpContext;

        if (context is null)
        {
            return null;
        }

        var existing = context.Request.Cookies[CartCookieName];

        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        if (!createIfMissing || context.Response.HasStarted)
        {
            return null;
        }

        var key = Guid.NewGuid().ToString("N");

        context.Response.Cookies.Append(CartCookieName, key, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });

        return key;
    }

    private static void DeleteCartCookie(HttpContext context)
    {
        context.Response.Cookies.Delete(CartCookieName, new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps
        });
    }

    private async Task<(decimal ShippingFee, decimal FreeShippingThreshold)> GetShippingSettingsAsync(
        CancellationToken cancellationToken)
    {
        var values = await _dbContext.StoreSettings.AsNoTracking()
            .Where(s => s.Key == "DefaultShippingFee" || s.Key == "FreeShippingThreshold")
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        return (ReadDecimal(values, "DefaultShippingFee", 2m),
                ReadDecimal(values, "FreeShippingThreshold", 50m));
    }

    private static decimal ReadDecimal(
        IReadOnlyDictionary<string, string?> values,
        string key,
        decimal fallback)
    {
        return values.TryGetValue(key, out var raw)
               && decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static string BuildVariantLabel(ProductVariant? variant)
    {
        if (variant is null)
        {
            return string.Empty;
        }

        var parts = new[] { variant.Color, variant.Storage }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToList();

        return parts.Count == 0 ? "Standard" : string.Join(" / ", parts);
    }
}
