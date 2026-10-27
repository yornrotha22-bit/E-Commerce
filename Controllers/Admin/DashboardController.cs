using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RothaShop.Data;
using RothaShop.Localization;
using RothaShop.Models;
using RothaShop.Security;
using RothaShop.ViewModels;
namespace RothaShop.Controllers.Admin;
[Area("Admin")]
[Authorize(Policy = Policies.AdminArea)]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITranslator _t;
    public DashboardController(ApplicationDbContext dbContext, ITranslator translator)
    {
        _dbContext = dbContext;
        _t = translator;
    }
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var customerRoleId = await _dbContext.Roles
            .Where(r => r.Name == Roles.Customer)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var model = new DashboardViewModel
        {
            TotalSales = await _dbContext.Orders
                .Where(o => o.PaymentStatus == PaymentStatus.Paid)
                .SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0m,

            OrderCount = await _dbContext.Orders.CountAsync(cancellationToken),

            PendingOrders = await _dbContext.Orders
                .CountAsync(o => o.OrderStatus == OrderStatus.Pending, cancellationToken),

            CustomerCount = customerRoleId is null
                ? 0
                : await _dbContext.UserRoles.CountAsync(ur => ur.RoleId == customerRoleId, cancellationToken),

            ProductCount = await _dbContext.Products
                .CountAsync(p => p.IsActive, cancellationToken),

            LowStockCount = await _dbContext.ProductVariants
                .CountAsync(v => v.IsActive && v.StockQuantity <= v.LowStockThreshold, cancellationToken),

            RecentOrders = await _dbContext.Orders.AsNoTracking()
                .OrderByDescending(o => o.CreatedAt)
                .Take(8)
                .Select(o => new RecentOrderRow
                {
                    Id = o.Id,
                    OrderNumber = o.OrderNumber,
                    CustomerName = o.CustomerName,
                    TotalAmount = o.TotalAmount,
                    OrderStatus = o.OrderStatus,
                    PaymentStatus = o.PaymentStatus,
                    CreatedAt = o.CreatedAt
                })
                .ToListAsync(cancellationToken),

            LowStockProducts = await _dbContext.ProductVariants.AsNoTracking()
                .Where(v => v.IsActive && v.StockQuantity <= v.LowStockThreshold)
                .OrderBy(v => v.StockQuantity)
                .Take(8)
                .Select(v => new LowStockRow
                {
                    VariantId = v.Id,
                    ProductId = v.ProductId,
                    ProductName = v.Product != null ? v.Product.Name : string.Empty,
                    SKU = v.SKU,
                    VariantDescription = (v.Color != null ? "Color: " + v.Color : string.Empty)
                        + (v.Storage != null ? " / Storage: " + v.Storage : string.Empty),
                    StockQuantity = v.StockQuantity,
                    LowStockThreshold = v.LowStockThreshold
                })
                .ToListAsync(cancellationToken)
        };

        ViewData["Title"] = _t["admin.dashboardTitle"];
        ViewData["Active"] = "Dashboard";

        // Real paid-sales series for the last seven UTC days (zero-filled).
        var today = DateTime.UtcNow.Date;
        var weekStart = today.AddDays(-6);

        var paidByDay = await _dbContext.Orders.AsNoTracking()
            .Where(o => o.PaymentStatus == PaymentStatus.Paid && o.CreatedAt >= weekStart)
            .GroupBy(o => o.CreatedAt.Date)
            .Select(g => new
            {
                Day = g.Key,
                Amount = g.Sum(o => o.TotalAmount),
                OrderCount = g.Count()
            })
            .ToListAsync(cancellationToken);

        var salesSeries = new List<DailySalesPoint>();

        for (var day = weekStart; day <= today; day = day.AddDays(1))
        {
            var match = paidByDay.FirstOrDefault(x => x.Day == day);

            salesSeries.Add(new DailySalesPoint
            {
                Date = day,
                Amount = match?.Amount ?? 0m,
                OrderCount = match?.OrderCount ?? 0
            });
        }

        model.SalesLast7Days = salesSeries;

        return View(model);
    }
}
