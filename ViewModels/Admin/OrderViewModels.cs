using Microsoft.AspNetCore.Mvc.Rendering;
using RothaShop.Models;

namespace RothaShop.ViewModels.Admin;

public class AdminOrderRow
{
    public int Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerPhone { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public OrderStatus OrderStatus { get; set; }

    public PaymentStatus PaymentStatus { get; set; }

    public decimal TotalAmount { get; set; }

    public int ItemCount { get; set; }
}

public class AdminOrderListViewModel
{
    public string? Search { get; set; }

    public OrderStatus? Status { get; set; }

    public PaymentStatus? PaymentStatus { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public int TotalItems { get; set; }

    public int TotalPages => PageSize <= 0
        ? 1
        : (int)Math.Ceiling(TotalItems / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;

    public IReadOnlyList<AdminOrderRow> Items { get; set; } = Array.Empty<AdminOrderRow>();

    public IReadOnlyList<SelectListItem> StatusOptions { get; set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> PaymentStatusOptions { get; set; } = Array.Empty<SelectListItem>();
}

public class AdminOrderDetailsViewModel
{
    public Order Order { get; set; } = new();

    public IReadOnlyList<OrderStatus> AllowedNextStatuses { get; set; } = Array.Empty<OrderStatus>();

    public bool CanCancel { get; set; }

    public decimal PaidAmount { get; set; }
}
