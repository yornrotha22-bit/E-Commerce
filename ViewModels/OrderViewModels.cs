using RothaShop.Models;
using RothaShop.Services;

namespace RothaShop.ViewModels;

public class OrderListItemViewModel
{
    public int Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public OrderStatus OrderStatus { get; set; }

    public PaymentStatus PaymentStatus { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public decimal TotalAmount { get; set; }

    public int ItemCount { get; set; }
}

public class OrderDetailsViewModel
{
    public Order Order { get; set; } = new();

    public bool CanCancel { get; set; }

    public PaymentInstructions? PaymentInstructions { get; set; }
}
