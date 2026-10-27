using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RothaShop.Data;
using RothaShop.Localization;
using RothaShop.Models;
using RothaShop.Security;
using RothaShop.Services;
using RothaShop.ViewModels.Admin;

namespace RothaShop.Controllers.Admin;

[Area("Admin")]
[Authorize(Policy = Policies.ManageOrders)]
public class OrdersController : Controller
{
    private const int PageSize = 20;

    private readonly ApplicationDbContext _dbContext;
    private readonly IOrderService _orderService;
    private readonly IAuditService _auditService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITranslator _t;

    public OrdersController(
        ApplicationDbContext dbContext,
        IOrderService orderService,
        IAuditService auditService,
        UserManager<ApplicationUser> userManager,
        ITranslator translator)
    {
        _dbContext = dbContext;
        _orderService = orderService;
        _auditService = auditService;
        _userManager = userManager;
        _t = translator;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        OrderStatus? status,
        PaymentStatus? paymentStatus,
        int page = 1,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        var effectivePageSize = AdminPageSize.Normalize(pageSize, PageSize);

        var query = _dbContext.Orders.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            
var pattern = SearchPattern.Contains(term);
            query = query.Where(o =>
                EF.Functions.ILike(o.OrderNumber, pattern)
                || EF.Functions.ILike(o.CustomerName, pattern)
                || EF.Functions.ILike(o.CustomerPhone, pattern));
        }

        if (status.HasValue)
        {
            query = query.Where(o => o.OrderStatus == status.Value);
        }

        if (paymentStatus.HasValue)
        {
            query = query.Where(o => o.PaymentStatus == paymentStatus.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)effectivePageSize);
        page = Math.Min(page, totalPages);

        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .Select(o => new AdminOrderRow
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                CustomerName = o.CustomerName,
                CustomerPhone = o.CustomerPhone,
                CreatedAt = o.CreatedAt,
                OrderStatus = o.OrderStatus,
                PaymentStatus = o.PaymentStatus,
                TotalAmount = o.TotalAmount,
                ItemCount = o.Items.Sum(i => i.Quantity)
            })
            .ToListAsync(cancellationToken);

        var model = new AdminOrderListViewModel
        {
            Search = search,
            Status = status,
            PaymentStatus = paymentStatus,
            Page = page,
            PageSize = effectivePageSize,
            TotalItems = totalItems,
            Items = items
        };

        ViewData["Title"] = "Orders";
        ViewData["Active"] = "Orders";

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders.AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Include(o => o.StatusHistory.OrderBy(h => h.CreatedAt))
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order is null)
        {
            return NotFound();
        }

        var canCancel = order.OrderStatus is OrderStatus.Pending or OrderStatus.Confirmed or OrderStatus.Processing
                        && order.PaymentStatus != PaymentStatus.Paid;

        ViewData["Title"] = $"Order {order.OrderNumber}";
        ViewData["Active"] = "Orders";

        return View(new AdminOrderDetailsViewModel
        {
            Order = order,
            AllowedNextStatuses = OrderStatusFlow.Next(order.OrderStatus)
                .Where(s => s != OrderStatus.Cancelled)
                .ToList(),
            CanCancel = canCancel,
            PaidAmount = order.Payments
                .Where(p => p.Status == PaymentStatus.Paid)
                .Sum(p => p.Amount)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(
        int id,
        OrderStatus newStatus,
        string? carrier,
        string? trackingNumber,
        string? note,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = _t["msg.orderStatusInvalid"];
            return RedirectToAction(nameof(Details), new { id });
        }

        var staffUserId = _userManager.GetUserId(User);
        var result = await _orderService.UpdateStatusAsync(
            id,
            newStatus,
            carrier,
            trackingNumber,
            note,
            staffUserId,
            cancellationToken);

        if (result.Success)
        {
            await _auditService.LogAsync(
                "OrderStatusChanged",
                nameof(Order),
                id.ToString(),
                result.Message,
                cancellationToken);
        }

        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateShipping(
        int id,
        string? carrier,
        string? trackingNumber,
        CancellationToken cancellationToken)
    {
        var staffUserId = _userManager.GetUserId(User);
        var result = await _orderService.UpdateShippingAsync(
            id,
            carrier,
            trackingNumber,
            staffUserId,
            cancellationToken);

        if (result.Success)
        {
            await _auditService.LogAsync(
                "OrderShippingUpdated",
                nameof(Order),
                id.ToString(),
                result.Message,
                cancellationToken);
        }

        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason, CancellationToken cancellationToken)
    {
        var staffUserId = _userManager.GetUserId(User);
        var result = await _orderService.CancelOrderByStaffAsync(id, staffUserId, reason ?? string.Empty, cancellationToken);

        if (result.Success)
        {
            await _auditService.LogAsync(
                "OrderCancelled",
                nameof(Order),
                id.ToString(),
                result.Message,
                cancellationToken);
        }

        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }
}
