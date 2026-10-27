using Microsoft.AspNetCore.Mvc;
using RothaShop.Localization;
using RothaShop.Services;

namespace RothaShop.Controllers;

public class CartController : Controller
{
    private readonly ICartService _cartService;
    private readonly ITranslator _t;

    public CartController(ICartService cartService, ITranslator translator)
    {
        _cartService = cartService;
        _t = translator;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Title"] = _t["cartPage.title"];
        return View(await _cartService.GetCartAsync(cancellationToken));
    }

    /// <summary>
    /// Body of the slide-out cart. Rendered server-side so the drawer always shows
    /// the same prices, stock state and totals as the cart page.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Drawer(CancellationToken cancellationToken)
    {
        return PartialView("_CartDrawer", await _cartService.GetCartAsync(cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(
        int productVariantId,
        int quantity = 1,
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _cartService.AddAsync(productVariantId, quantity, cancellationToken);

        if (IsAjaxRequest())
        {
            return await JsonCartResultAsync(result, cancellationToken);
        }

        TempData["CartMessage"] = result.Message;
        TempData["CartMessageType"] = result.Success ? "success" : "danger";

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        int cartItemId,
        int quantity,
        CancellationToken cancellationToken)
    {
        var result = await _cartService.UpdateQuantityAsync(cartItemId, quantity, cancellationToken);

        TempData["CartMessage"] = result.Message;
        TempData["CartMessageType"] = result.Success ? "success" : "danger";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int cartItemId, CancellationToken cancellationToken)
    {
        var result = await _cartService.RemoveAsync(cartItemId, cancellationToken);

        if (IsAjaxRequest())
        {
            return await JsonCartResultAsync(result, cancellationToken);
        }

        TempData["CartMessage"] = result.Message;
        TempData["CartMessageType"] = result.Success ? "success" : "danger";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        await _cartService.ClearAsync(cancellationToken);
        TempData["CartMessage"] = _t["msg.cartNowEmpty"];
        TempData["CartMessageType"] = "success";

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Progressive enhancement only: the storefront JavaScript sets this header,
    /// so browsers without script keep the redirect + TempData toast flow.
    /// </summary>
    private bool IsAjaxRequest()
    {
        return string.Equals(
            Request.Headers["X-Requested-With"],
            "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase);
    }

    private async Task<IActionResult> JsonCartResultAsync(
        CartOperationResult result,
        CancellationToken cancellationToken)
    {
        var cart = await _cartService.GetCartAsync(cancellationToken);

        return Json(new
        {
            success = result.Success,
            message = result.Message,
            count = cart.ItemCount,
            subtotal = cart.Subtotal
        });
    }
}
