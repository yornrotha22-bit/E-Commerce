using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RothaShop.Data;
using RothaShop.Localization;
using RothaShop.Models;
using RothaShop.Security;
using RothaShop.ViewModels.Admin;

namespace RothaShop.Controllers.Admin;

[Area("Admin")]
[Authorize(Policy = Policies.ViewReports)]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITranslator _t;

    public ReportsController(ApplicationDbContext dbContext, ITranslator translator)
    {
        _dbContext = dbContext;
        _t = translator;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken)
    {
        var toDate = (to ?? DateTime.UtcNow).Date;
        var fromDate = (from ?? toDate.AddDays(-29)).Date;

        if (fromDate > toDate)
        {
            (fromDate, toDate) = (toDate, fromDate);
            ViewData["RangeNotice"] = _t["msg.rangeSwapped"];
        }

        if ((toDate - fromDate).TotalDays > 366)
        {
            fromDate = toDate.AddDays(-366);
            ViewData["RangeNotice"] = _t["msg.rangeLimited"];
        }

        var startUtc = DateTime.SpecifyKind(fromDate, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(toDate, DateTimeKind.Utc).AddDays(1);

        var orders = _dbContext.Orders.AsNoTracking()
            .Where(o => o.CreatedAt >= startUtc && o.CreatedAt < endUtc);

        var paidOrders = orders.Where(o => o.PaymentStatus == PaymentStatus.Paid);

        var paidOrderCount = await paidOrders.CountAsync(cancellationToken);
        var paidRevenue = await paidOrders
            .SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0m;

        var model = new ReportViewModel
        {
            From = fromDate,
            To = toDate,
            OrderCount = await orders.CountAsync(cancellationToken),
            PaidOrderCount = paidOrderCount,
            PaidRevenue = paidRevenue,
            CancelledOrders = await orders.CountAsync(o => o.OrderStatus == OrderStatus.Cancelled, cancellationToken),
            AverageOrderValue = paidOrderCount == 0
                ? 0m
                : Math.Round(paidRevenue / paidOrderCount, 2, MidpointRounding.AwayFromZero),
            UnitsSold = await _dbContext.OrderItems.AsNoTracking()
                .Where(i => i.Order!.PaymentStatus == PaymentStatus.Paid
                            && i.Order.CreatedAt >= startUtc
                            && i.Order.CreatedAt < endUtc)
                .SumAsync(i => (int?)i.Quantity, cancellationToken) ?? 0,

            DailySales = await paidOrders
                .GroupBy(o => o.CreatedAt.Date)
                .Select(g => new DailySalesRow
                {
                    Date = g.Key,
                    Orders = g.Count(),
                    Revenue = g.Sum(o => o.TotalAmount)
                })
                .OrderBy(r => r.Date)
                .ToListAsync(cancellationToken),

            TopProducts = await _dbContext.OrderItems.AsNoTracking()
                .Where(i => i.Order!.PaymentStatus == PaymentStatus.Paid
                            && i.Order.CreatedAt >= startUtc
                            && i.Order.CreatedAt < endUtc)
                .GroupBy(i => new { i.ProductId, i.ProductName, i.SKU })
                .Select(g => new TopProductRow
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.ProductName,
                    SKU = g.Key.SKU,
                    Quantity = g.Sum(i => i.Quantity),
                    Revenue = g.Sum(i => i.LineTotal)
                })
                .OrderByDescending(r => r.Revenue)
                .Take(10)
                .ToListAsync(cancellationToken),

            PaymentBreakdown = await paidOrders
                .GroupBy(o => o.PaymentMethod)
                .Select(g => new PaymentMethodRow
                {
                    Method = g.Key,
                    Count = g.Count(),
                    Amount = g.Sum(o => o.TotalAmount)
                })
                .OrderByDescending(r => r.Amount)
                .ToListAsync(cancellationToken),

            TopCustomers = await paidOrders
                .GroupBy(o => new { o.UserId, o.CustomerName })
                .Select(g => new TopCustomerRow
                {
                    CustomerId = g.Key.UserId,
                    CustomerName = g.Key.CustomerName,
                    Orders = g.Count(),
                    Revenue = g.Sum(o => o.TotalAmount)
                })
                .OrderByDescending(r => r.Revenue)
                .Take(10)
                .ToListAsync(cancellationToken)
        };

        ViewData["Title"] = _t["admin.nav.reports"];
        ViewData["Active"] = "Reports";

        return View(model);
    }
}
