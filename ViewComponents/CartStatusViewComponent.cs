using Microsoft.AspNetCore.Mvc;
using RothaShop.Services;
namespace RothaShop.ViewComponents;
/// <summary>
/// Shows the number of items in the current cart (user cart or guest cart).
/// </summary>
public class CartStatusViewComponent : ViewComponent
{
    private readonly ICartService _cartService;
    public CartStatusViewComponent(ICartService cartService)
    {
        _cartService = cartService;
    }
    public async Task<IViewComponentResult> InvokeAsync(CancellationToken cancellationToken = default)
    {
        var count = await _cartService.GetItemCountAsync(cancellationToken);
        return View(count);
    }
}
