using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RothaShop.Data;
using RothaShop.Localization;
using RothaShop.Models;
using RothaShop.Services;
using RothaShop.ViewModels;

namespace RothaShop.Controllers;

[Authorize]
public class CheckoutController : Controller
{
    private const string CouponSessionKey = "Checkout.CouponCode";

    private readonly ICartService _cartService;
    private readonly IOrderService _orderService;
    private readonly IPaymentService _paymentService;
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITranslator _t;
    private readonly ILogger<CheckoutController> _logger;

    public CheckoutController(
        ICartService cartService,
        IOrderService orderService,
        IPaymentService paymentService,
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ITranslator translator,
        ILogger<CheckoutController> logger)
    {
        _cartService = cartService;
        _orderService = orderService;
        _paymentService = paymentService;
        _dbContext = dbContext;
        _userManager = userManager;
        _t = translator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await BuildViewModelAsync(null, cancellationToken);

        if (model.Cart.IsEmpty)
        {
            TempData["CartMessage"] = _t["msg.checkoutCartEmpty"];
            TempData["CartMessageType"] = "danger";
            return RedirectToAction("Index", "Cart");
        }

        if (model.Cart.HasUnavailableItems)
        {
            TempData["CartMessage"] = _t["msg.checkoutCartUnavailable"];
            TempData["CartMessageType"] = "danger";
            return RedirectToAction("Index", "Cart");
        }

        ViewData["Title"] = _t["checkout.title"];
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyCoupon(string? couponCode, CancellationToken cancellationToken)
    {
        var cart = await _cartService.GetCartAsync(cancellationToken);
        var result = await _orderService.ValidateCouponAsync(couponCode, cart.Subtotal, cancellationToken);

        if (result.IsValid)
        {
            HttpContext.Session.SetString(CouponSessionKey, result.Code!);
            TempData["CouponNotice"] = result.Message;
            TempData["CouponNoticeType"] = "success";
        }
        else
        {
            HttpContext.Session.Remove(CouponSessionKey);
            TempData["CouponNotice"] = result.Message ?? _t["msg.enterCouponCode"];
            TempData["CouponNoticeType"] = "danger";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RemoveCoupon()
    {
        HttpContext.Session.Remove(CouponSessionKey);
        TempData["CouponNotice"] = _t["msg.couponRemoved"];
        TempData["CouponNoticeType"] = "success";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlaceOrder(
        CheckoutViewModel model,
        CancellationToken cancellationToken)
    {
        ViewData["Title"] = _t["checkout.title"];

        var paymentProvider = _paymentService.GetProvider(model.PaymentMethod);

        if (!paymentProvider.IsAvailable)
        {
            ModelState.AddModelError(
                nameof(model.PaymentMethod),
                paymentProvider.UnavailableReason ?? "This payment method is not available.");
        }

        var cart = await _cartService.GetCartAsync(cancellationToken);

        if (cart.IsEmpty)
        {
            ModelState.AddModelError(string.Empty, "Your cart is empty.");
        }
        else if (cart.HasUnavailableItems)
        {
            ModelState.AddModelError(
                string.Empty,
                "Some cart items are unavailable. Update your cart before checking out.");
        }

        if (!ModelState.IsValid)
        {
            model = await BuildViewModelAsync(model, cancellationToken);
            return View("Index", model);
        }

        var userId = _userManager.GetUserId(User);

        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var command = new PlaceOrderCommand
        {
            CustomerName = model.FullName,
            CustomerPhone = model.PhoneNumber,
            CustomerEmail = model.Email,
            AddressLine1 = model.AddressLine1,
            AddressLine2 = model.AddressLine2,
            City = model.City,
            State = model.State,
            PostalCode = model.PostalCode,
            Country = model.Country,
            Notes = model.Notes,
            CouponCode = HttpContext.Session.GetString(CouponSessionKey),
            PaymentMethod = model.PaymentMethod,
            SaveAddress = model.SaveAddress
        };

        var result = await _orderService.PlaceOrderAsync(userId, command, cancellationToken);

        if (!result.Success)
        {
            ModelState.AddModelError(result.ErrorField ?? string.Empty, result.Message);
            model = await BuildViewModelAsync(model, cancellationToken);
            return View("Index", model);
        }

        HttpContext.Session.Remove(CouponSessionKey);

        TempData["OrderSuccess"] = _t.Format(
            "msg.orderPlacedThanks",
            result.OrderNumber,
            _t["msg.paymentCodName"]);

        _logger.LogInformation("Checkout completed for order {OrderNumber}.", result.OrderNumber);

        return RedirectToAction("Details", "Orders", new { id = result.OrderId });
    }

    private async Task<CheckoutViewModel> BuildViewModelAsync(
        CheckoutViewModel? existingModel,
        CancellationToken cancellationToken)
    {
        var cart = await _cartService.GetCartAsync(cancellationToken);
        var userId = _userManager.GetUserId(User);
        var user = await _userManager.GetUserAsync(User);

        var addresses = string.IsNullOrEmpty(userId)
            ? new List<Address>()
            : await _dbContext.Addresses.AsNoTracking()
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.Id)
                .ToListAsync(cancellationToken);

        var couponCode = HttpContext.Session.GetString(CouponSessionKey);
        var coupon = await _orderService.ValidateCouponAsync(couponCode, cart.Subtotal, cancellationToken);

        if (!coupon.IsValid && !string.IsNullOrWhiteSpace(couponCode))
        {
            HttpContext.Session.Remove(CouponSessionKey);
        }

        var model = existingModel ?? new CheckoutViewModel();

        var onlineProvider = _paymentService.GetProvider(PaymentMethod.OnlinePayment);

        model.Cart = cart;
        model.SavedAddresses = addresses;
        model.OnlinePaymentAvailable = onlineProvider.IsAvailable;
        model.OnlinePaymentUnavailableReason = onlineProvider.UnavailableReason;
        model.CouponCode = coupon.IsValid ? coupon.Code : null;
        model.CouponApplied = coupon.IsValid;
        model.DiscountAmount = coupon.DiscountAmount;
        model.CouponMessage = coupon.Message;

        if (existingModel is null)
        {
            var defaultAddress = addresses.FirstOrDefault();

            model.FullName = defaultAddress?.FullName ?? user?.FullName ?? string.Empty;
            model.PhoneNumber = defaultAddress?.PhoneNumber ?? user?.PhoneNumber ?? string.Empty;
            model.Email = user?.Email;
            model.AddressLine1 = defaultAddress?.AddressLine1 ?? string.Empty;
            model.AddressLine2 = defaultAddress?.AddressLine2;
            model.City = defaultAddress?.City ?? string.Empty;
            model.State = defaultAddress?.State;
            model.PostalCode = defaultAddress?.PostalCode;
            model.Country = defaultAddress?.Country ?? "Cambodia";
            model.SelectedAddressId = defaultAddress?.Id;
            model.SaveAddress = addresses.Count == 0;
        }

        return model;
    }
}
