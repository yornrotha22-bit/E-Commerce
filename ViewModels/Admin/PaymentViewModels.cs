using RothaShop.Models;

namespace RothaShop.ViewModels.Admin;

public class AdminPaymentRow
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public PaymentMethod Method { get; set; }

    public string Provider { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public PaymentStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? PaidAt { get; set; }
}
