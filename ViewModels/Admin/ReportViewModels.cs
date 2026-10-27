using RothaShop.Models;

namespace RothaShop.ViewModels.Admin;

public class ReportViewModel
{
    public DateTime From { get; set; }

    public DateTime To { get; set; }

    public decimal PaidRevenue { get; set; }

    public int OrderCount { get; set; }

    public int PaidOrderCount { get; set; }

    public int CancelledOrders { get; set; }

    public int UnitsSold { get; set; }

    public decimal AverageOrderValue { get; set; }

    public IReadOnlyList<DailySalesRow> DailySales { get; set; } = Array.Empty<DailySalesRow>();

    public IReadOnlyList<TopProductRow> TopProducts { get; set; } = Array.Empty<TopProductRow>();

    public IReadOnlyList<PaymentMethodRow> PaymentBreakdown { get; set; } = Array.Empty<PaymentMethodRow>();

    public IReadOnlyList<TopCustomerRow> TopCustomers { get; set; } = Array.Empty<TopCustomerRow>();

    public decimal MaxDailyRevenue => DailySales.Count == 0
        ? 0m
        : DailySales.Max(d => d.Revenue);
}

public class DailySalesRow
{
    public DateTime Date { get; set; }

    public int Orders { get; set; }

    public decimal Revenue { get; set; }
}

public class TopProductRow
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string SKU { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal Revenue { get; set; }
}

public class PaymentMethodRow
{
    public PaymentMethod Method { get; set; }

    public int Count { get; set; }

    public decimal Amount { get; set; }
}

public class TopCustomerRow
{
    public string CustomerId { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public int Orders { get; set; }

    public decimal Revenue { get; set; }
}
