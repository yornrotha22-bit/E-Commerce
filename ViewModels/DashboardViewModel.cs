using RothaShop.Models;

namespace RothaShop.ViewModels;

public class DashboardViewModel
{
    public decimal TotalSales { get; set; }

    public int OrderCount { get; set; }

    public int PendingOrders { get; set; }

    public int CustomerCount { get; set; }

    public int ProductCount { get; set; }

    public int LowStockCount { get; set; }

    /// <summary>
    /// Paid sales per UTC day for the last seven days. Values come straight from
    /// the Orders table; days without sales are returned as zero.
    /// </summary>
    public IReadOnlyList<DailySalesPoint> SalesLast7Days { get; set; } = Array.Empty<DailySalesPoint>();

    public decimal SalesLast7DaysTotal => SalesLast7Days.Sum(d => d.Amount);

    public IReadOnlyList<RecentOrderRow> RecentOrders { get; set; } = Array.Empty<RecentOrderRow>();

    public IReadOnlyList<LowStockRow> LowStockProducts { get; set; } = Array.Empty<LowStockRow>();
}

public class DailySalesPoint
{
    public DateTime Date { get; set; }

    public decimal Amount { get; set; }

    public int OrderCount { get; set; }
}

public class RecentOrderRow
{
    public int Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }

    public OrderStatus OrderStatus { get; set; }

    public PaymentStatus PaymentStatus { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class LowStockRow
{
    public int VariantId { get; set; }

    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string SKU { get; set; } = string.Empty;

    public string? VariantDescription { get; set; }

    public int StockQuantity { get; set; }

    public int LowStockThreshold { get; set; }
}
