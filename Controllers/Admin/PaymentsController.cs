using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RothaShop.Data;
using RothaShop.Localization;
using RothaShop.Models;
using RothaShop.Security;
using RothaShop.Services;
using RothaShop.ViewModels;
using RothaShop.ViewModels.Admin;

namespace RothaShop.Controllers.Admin;

[Area("Admin")]
[Authorize(Policy = Policies.ManagePayments)]
public class PaymentsController : Controller
{
    private const int PageSize = 20;

    private readonly ApplicationDbContext _dbContext;
    private readonly IPaymentService _paymentService;
    private readonly IAuditService _auditService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITranslator _t;

    public PaymentsController(
        ApplicationDbContext dbContext,
        IPaymentService paymentService,
        IAuditService auditService,
        UserManager<ApplicationUser> userManager,
        ITranslator translator)
    {
        _dbContext = dbContext;
        _paymentService = paymentService;
        _auditService = auditService;
        _userManager = userManager;
        _t = translator;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        PaymentStatus? status,
        int page = 1,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        var effectivePageSize = AdminPageSize.Normalize(pageSize, PageSize);

        var query = _dbContext.Payments.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            
var pattern = SearchPattern.Contains(term);
            query = query.Where(p =>
                EF.Functions.ILike(p.Order!.OrderNumber, pattern)
                || EF.Functions.ILike(p.Order.CustomerName, pattern)
                || EF.Functions.ILike(p.Provider, pattern));
        }

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)effectivePageSize);
        page = Math.Min(page, totalPages);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .Select(p => new AdminPaymentRow
            {
                Id = p.Id,
                OrderId = p.OrderId,
                OrderNumber = p.Order!.OrderNumber,
                CustomerName = p.Order.CustomerName,
                Method = p.Method,
                Provider = p.Provider,
                Amount = p.Amount,
                Currency = p.Currency,
                Status = p.Status,
                CreatedAt = p.CreatedAt,
                PaidAt = p.PaidAt
            })
            .ToListAsync(cancellationToken);

        ViewData["Title"] = _t["admin.nav.payments"];
        ViewData["Active"] = "Payments";
        ViewData["Search"] = search;
        ViewData["Status"] = status;

        return View(new PagedList<AdminPaymentRow>(items, page, effectivePageSize, totalItems));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkPaid(int id, CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        var result = await _paymentService.MarkPaidAsync(id, userId, cancellationToken);

        if (result.Success)
        {
            await _auditService.LogAsync(
                "PaymentPaid",
                nameof(Payment),
                id.ToString(),
                result.Message,
                cancellationToken);
        }

        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkFailed(int id, CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        var result = await _paymentService.MarkFailedAsync(
            id,
            userId,
            "Marked as failed by staff",
            cancellationToken);

        if (result.Success)
        {
            await _auditService.LogAsync(
                "PaymentFailed",
                nameof(Payment),
                id.ToString(),
                result.Message,
                cancellationToken);
        }

        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
