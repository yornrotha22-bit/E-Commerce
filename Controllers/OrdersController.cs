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
public class OrdersController : Controller
{
    private const int PageSize = 10;

    private readonly ApplicationDbContext _dbContext;
    private readonly IOrderService _orderService;
    private readonly IPaymentService _paymentService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITranslator _t;

    public OrdersController(
        ApplicationDbContext dbContext,
        IOrderService orderService,
        IPaymentService paymentService,
        UserManager<ApplicationUser> userManager,
        ITranslator translator)
    {
        _dbContext = dbContext;
        _orderService = orderService;
        _paymentService = paymentService;
        _userManager = userManager;
        _t = translator;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        var userId = _userManager.GetUserId(User);

        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        page = Math.Max(page, 1);

        var query = _dbContext.Orders.AsNoTracking().Where(o => o.UserId == userId);

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)PageSize);
        page = Math.Min(page, totalPages);

        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(o => new OrderListItemViewModel
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                CreatedAt = o.CreatedAt,
                OrderStatus = o.OrderStatus,
                PaymentStatus = o.PaymentStatus,
                TotalAmount = o.TotalAmount,
                ItemCount = o.Items.Sum(i => i.Quantity)
            })
            .ToListAsync(cancellationToken);

        ViewData["Title"] = _t["orders.title"];
        ViewData["AccountTab"] = "Orders";

        return View(new PagedList<OrderListItemViewModel>(items, page, PageSize, totalItems));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);

        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        // Ownership is part of the query: other customers' orders return 404.
        var order = await _dbContext.Orders.AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Include(o => o.StatusHistory.OrderBy(h => h.CreatedAt))
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId, cancellationToken);

        if (order is null)
        {
            return NotFound();
        }

        ViewData["Title"] = _t.Format("order.title", order.OrderNumber);
        ViewData["AccountTab"] = "Orders";

        var canCancel = order.OrderStatus is OrderStatus.Pending or OrderStatus.Confirmed
                        && order.PaymentStatus != PaymentStatus.Paid;

        PaymentInstructions? instructions = null;

        if (order.PaymentMethod == PaymentMethod.OnlinePayment
            && order.PaymentStatus == PaymentStatus.Pending)
        {
            instructions = await _paymentService.GetInstructionsAsync(order.Id, cancellationToken);
        }

        return View(new OrderDetailsViewModel
        {
            Order = order,
            CanCancel = canCancel,
            PaymentInstructions = instructions
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckBakongPayment(int id, CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var result = await _paymentService.CheckBakongPaymentAsync(id, userId, cancellationToken);
        return result is null
            ? NotFound()
            : Json(new { status = result.Status.ToString(), message = result.Message });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);

        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var result = await _orderService.CancelOrderAsync(id, userId, cancellationToken);

        TempData["OrderMessage"] = result.Message;
        TempData["OrderMessageType"] = result.Success ? "success" : "danger";

        return RedirectToAction(nameof(Details), new { id });
    }
}
