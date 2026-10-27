using RothaShop.Models;

namespace RothaShop.ViewModels;

/// <summary>
/// Everything the signed-in customer needs at a glance. Counts and previews are
/// read from the same services and tables that power the individual account pages,
/// so the dashboard can never drift from them.
/// </summary>
public class CustomerDashboardViewModel
{
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public DateTime MemberSince { get; set; }

    public int OrderCount { get; set; }

    /// <summary>Pending, confirmed, processing or shipped orders.</summary>
    public int OpenOrderCount { get; set; }

    /// <summary>Sum of orders whose payment was verified as paid.</summary>
    public decimal TotalSpent { get; set; }

    public int WishlistCount { get; set; }

    public IReadOnlyList<WishlistRowViewModel> WishlistPreview { get; set; } = Array.Empty<WishlistRowViewModel>();

    public int AddressCount { get; set; }

    public Address? DefaultAddress { get; set; }

    public int CartItemCount { get; set; }

    public IReadOnlyList<OrderListItemViewModel> RecentOrders { get; set; } = Array.Empty<OrderListItemViewModel>();

    /// <summary>Newest order that still needs something from the customer.</summary>
    public OrderListItemViewModel? ActionNeededOrder { get; set; }

    /// <summary>Online orders waiting for a payment the customer still has to make.</summary>
    public bool ActionNeededIsPayment =>
        ActionNeededOrder is not null
        && ActionNeededOrder.PaymentMethod == PaymentMethod.OnlinePayment
        && ActionNeededOrder.PaymentStatus == PaymentStatus.Pending;
}
